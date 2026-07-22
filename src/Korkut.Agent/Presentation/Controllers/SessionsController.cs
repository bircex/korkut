using System.Text.Json;
using Korkut.Agent.Tenancy;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Agent.Domain.Conversations;
using Korkut.Agent.Presentation.Contracts.Sessions;
using Korkut.Application.Abstractions.Directory;
using Korkut.Application.Ai;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Agent.Presentation.Controllers;

/// <summary>
/// The assistant's conversations ("sessions") + chat, served by the Agent host as a lightweight
/// controller. Authenticated, tenant-scoped (RLS). Conversation state lives in the Agent DB; the
/// persona / style / memory a chat turn composes come from the operational DB, so chat/upload/stream
/// pin BOTH contexts (see <see cref="AgentScope"/>). Reads use matters.read; create/chat/rename/
/// clone/delete use matters.write. Chat returns 503 when no model provider is configured.
/// </summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    AgentDbContext db, ISessionService svc, IUserLookup dir, SessionChatService chat, KorkutDbContext office)
    : ControllerBase
{
    // SSE frame payloads use the same snake_case contract as the rest of the API.
    private static readonly JsonSerializerOptions SseJson =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? 20, 1, 100);
        var skip = Math.Max(offset ?? 0, 0);
        var (items, total) = await User.InTenantScopeAsync(db, o => svc.ListAsync(o, take, skip, ct), ct);
        var names = await ResolveNamesAsync(dir, User.OfficeId(), items, ct);
        return Ok(new SessionPageResponse(items.Select(b => ToBrief(b, names)).ToList(), total));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromQuery] Guid? matterId, CancellationToken ct)
    {
        var uid = User.UserId();
        var brief = await User.InTenantScopeAsync(db, async o =>
        {
            var id = await svc.CreateAsync(o, uid, matterId, ct);
            return await svc.GetBriefAsync(o, id, ct);
        }, ct);
        return Created($"/api/sessions/{brief!.Id}", await ToBriefAsync(dir, User.OfficeId(), brief, ct));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await User.InTenantScopeAsync(db, async o =>
        {
            var brief = await svc.GetBriefAsync(o, id, ct);
            if (brief is null)
            {
                return ((SessionBrief Brief, List<ChatMessageResponse> Messages)?)null;
            }

            var messages = (await svc.MessagesAsync(o, id, ct)).Select(m => new ChatMessageResponse(m.Role, m.Content)).ToList();
            return (brief, messages);
        }, ct);

        if (result is not { } data)
        {
            throw ApiException.NotFound("Görüşme bulunamadı.");
        }

        var name = await ResolveNameAsync(dir, User.OfficeId(), data.Brief.CreatedBy, ct);
        return Ok(new SessionDetailResponse(
            data.Brief.Id, data.Brief.Title, data.Brief.MatterId, name, data.Brief.UpdatedAt, data.Messages));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameSessionRequest body, CancellationToken ct)
    {
        var brief = await User.InTenantScopeAsync(db, async o =>
            await svc.RenameAsync(o, id, body.Title!.Trim(), ct) ? await svc.GetBriefAsync(o, id, ct) : null, ct);
        return brief is null
            ? throw ApiException.NotFound("Görüşme bulunamadı.")
            : Ok(await ToBriefAsync(dir, User.OfficeId(), brief, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await User.InTenantScopeAsync(db, o => svc.DeleteAsync(o, id, ct), ct);
        return ok ? Ok(new { ok = true }) : throw ApiException.NotFound("Görüşme bulunamadı.");
    }

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct)
    {
        var uid = User.UserId();
        var brief = await User.InTenantScopeAsync(db, async o =>
        {
            var newId = await svc.CloneAsync(o, id, uid, ct);
            return newId is null ? null : await svc.GetBriefAsync(o, newId.Value, ct);
        }, ct);
        return brief is null
            ? throw ApiException.NotFound("Görüşme bulunamadı.")
            : Ok(await ToBriefAsync(dir, User.OfficeId(), brief, ct));
    }

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

        var text = form["text"].ToString();
        var uid = User.UserId();
        var perms = User.Permissions();
        try
        {
            await using var stream = file.OpenReadStream();
            var outcome = await User.InAgentAndOfficeScopeAsync(db, office, o =>
                chat.UploadAsync(o, uid, id, file.FileName, file.ContentType ?? "application/octet-stream", stream, text, perms, ct), ct);
            return outcome is null
                ? throw ApiException.NotFound("Görüşme bulunamadı.")
                : Ok(new { reply = outcome.Reply, title = outcome.Title, user_display = outcome.UserDisplay });
        }
        catch (ModelUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = ex.Message });
        }
    }

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

        var uid = User.UserId();
        var perms = User.Permissions();
        try
        {
            var outcome = await User.InAgentAndOfficeScopeAsync(db, office, o => chat.ChatStreamAsync(
                o, uid, id, body.Text!.Trim(), perms,
                label => Frame("step", new { label }),
                token => Frame("token", new { text = token }), ct), ct);

            if (outcome is null)
            {
                await Frame("error", new { detail = "Görüşme bulunamadı." });
            }
            else
            {
                await Frame("done", new ChatResponse(outcome.Reply, outcome.Title, outcome.OpenDocument, outcome.PendingEmail));
            }
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

    [HttpPost("{id:guid}/chat")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Chat(Guid id, [FromBody] ChatRequest body, CancellationToken ct)
    {
        var uid = User.UserId();
        var perms = User.Permissions();
        try
        {
            var outcome = await User.InAgentAndOfficeScopeAsync(db, office, o => chat.ChatAsync(o, uid, id, body.Text!.Trim(), perms, ct), ct);
            return outcome is null
                ? throw ApiException.NotFound("Görüşme bulunamadı.")
                : Ok(new ChatResponse(outcome.Reply, outcome.Title, outcome.OpenDocument, outcome.PendingEmail));
        }
        catch (ModelUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = ex.Message });
        }
    }

    private static async Task<SessionBriefResponse> ToBriefAsync(IUserLookup dir, Guid officeId, SessionBrief b, CancellationToken ct)
        => new(b.Id, b.Title, b.MatterId, await ResolveNameAsync(dir, officeId, b.CreatedBy, ct), b.UpdatedAt);

    private static SessionBriefResponse ToBrief(SessionBrief b, IReadOnlyDictionary<Guid, string> names)
        => new(b.Id, b.Title, b.MatterId, Lookup(names, b.CreatedBy), b.UpdatedAt);

    private static async Task<string?> ResolveNameAsync(IUserLookup dir, Guid officeId, Guid? author, CancellationToken ct)
    {
        if (author is not { } id)
        {
            return null;
        }

        var names = await dir.DisplayNamesAsync(officeId, [id], ct);
        return Lookup(names, author);
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(
        IUserLookup dir, Guid officeId, IReadOnlyList<SessionBrief> briefs, CancellationToken ct)
    {
        var ids = briefs.Where(b => b.CreatedBy is not null).Select(b => b.CreatedBy!.Value).Distinct().ToList();
        return await dir.DisplayNamesAsync(officeId, ids, ct);
    }

    private static string? Lookup(IReadOnlyDictionary<Guid, string> names, Guid? author)
        => author is { } id && names.TryGetValue(id, out var name) ? name : null;
}
