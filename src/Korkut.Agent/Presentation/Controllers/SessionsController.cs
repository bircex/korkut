using System.Text.Json;
using Korkut.Agent.Application.Chat;
using Korkut.Agent.Application.Contracts.Sessions;
using Korkut.Agent.Application.Sessions;
using Korkut.Application.Ai;
using Korkut.Application.Security;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Agent.Presentation.Controllers;

/// <summary>
/// The assistant's conversations ("sessions") + chat, served by the Agent host. A thin transport:
/// each action forwards the request to an application service (which owns the tenant scope,
/// orchestration, and response mapping) and returns the result. Reads use matters.read; create /
/// chat / rename / clone / delete use matters.write.
/// </summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(SessionAppService sessions, ChatAppService chat) : ControllerBase
{
    // SSE frame payloads use the same snake_case contract as the rest of the API.
    private static readonly JsonSerializerOptions SseJson =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
        => Ok(await sessions.ListAsync(limit, offset, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromQuery] Guid? matterId, CancellationToken ct)
    {
        var brief = await sessions.CreateAsync(matterId, ct);
        return Created($"/api/sessions/{brief.Id}", brief);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await sessions.GetAsync(id, ct));

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameSessionRequest body, CancellationToken ct)
        => Ok(await sessions.RenameAsync(id, body.Title!, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sessions.DeleteAsync(id, ct);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct)
        => Ok(await sessions.CloneAsync(id, ct));

    [HttpPost("{id:guid}/upload")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Upload(Guid id, CancellationToken ct)
    {
        if (!Request.HasFormContentType)
        {
            throw ApiException.BadRequest("multipart/form-data bekleniyor.");
        }

        var form = await Request.ReadFormAsync(ct);
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            throw ApiException.BadRequest("Dosya gerekli.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(await chat.UploadAsync(
            id, file.FileName, file.ContentType ?? "application/octet-stream", stream, form["text"].ToString(), ct));
    }

    [HttpPost("{id:guid}/chat")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Chat(Guid id, [FromBody] ChatRequest body, CancellationToken ct)
        => Ok(await chat.ChatAsync(id, body.Text!, ct));

    [HttpPost("{id:guid}/chat/stream")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> ChatStream(Guid id, [FromBody] ChatRequest body, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        async Task Frame(string ev, object data)
        {
            await Response.WriteAsync($"event: {ev}\ndata: {JsonSerializer.Serialize(data, SseJson)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        try
        {
            var outcome = await chat.StreamAsync(id, body.Text!,
                label => Frame("step", new { label }), token => Frame("token", new { text = token }), ct);
            await (outcome is null
                ? Frame("error", new { detail = "Görüşme bulunamadı." })
                : Frame("done", outcome));
        }
        catch (ModelUnavailableException ex)
        {
            await Frame("error", new { detail = ex.Message });
        }
        catch (BudgetExceededException ex)
        {
            await Frame("error", new { detail = ex.Message });
        }

        return new EmptyResult();
    }
}
