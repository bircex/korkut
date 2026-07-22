using Korkut.Agent.Services.Core.Documents;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Korkut.Application.Agent.Contracts;

namespace Korkut.Agent.Controllers;

/// <summary>
/// AI-drafted documents (the KDM document editor), served by the Agent host. A thin transport: each
/// action forwards to <see cref="DocumentAppService"/> (which owns the tenant scope, orchestration,
/// author resolution, and mapping) and returns the result. Reads matters.read; drafting / accept /
/// revert / save use matters.write. Export renders the current markdown to docx/UDF.
/// </summary>
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(IDocumentAppService docs) : ControllerBase
{
    [HttpGet("/api/sessions/{sessionId:guid}/document")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> ForSession(Guid sessionId, CancellationToken ct)
        => Ok(await docs.ForSessionAsync(sessionId, ct));

    [HttpPost("{docId:guid}/draft")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Draft(Guid docId, [FromBody] DraftRequest body, CancellationToken ct)
        => Ok(await docs.DraftAsync(docId, body.Instruction!, body.PetitionType, ct));

    [HttpPost("{docId:guid}/accept")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Accept(Guid docId, CancellationToken ct)
        => Ok(await docs.AcceptAsync(docId, ct));

    [HttpPost("{docId:guid}/revert")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Revert(Guid docId, CancellationToken ct)
        => Ok(await docs.RevertAsync(docId, ct));

    [HttpPut("{docId:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Save(Guid docId, [FromBody] SaveDocumentRequest body, CancellationToken ct)
        => Ok(await docs.SaveAsync(docId, body.Title!, body.ContentMd!, ct));

    [HttpGet("{docId:guid}/export")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Export(Guid docId, [FromQuery] string? format, CancellationToken ct)
    {
        var e = await docs.ExportAsync(docId, format, ct);
        return File(e.Bytes, e.ContentType, e.FileName);
    }
}
