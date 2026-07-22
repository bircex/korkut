using Korkut.Agent.Tenancy;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Agent.Presentation.Contracts.Documents;
using Korkut.Application.Abstractions.Directory;
using Korkut.Application.Ai;
using Korkut.Application.Security;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Agent.Presentation.Controllers;

/// <summary>
/// AI-drafted documents (the KDM document editor), served by the Agent host as a lightweight
/// controller. Authenticated, tenant-scoped (RLS). A document is created per session; the assistant
/// stages pending edits the user accepts or reverts, plus direct save and export. Document state
/// lives in the Agent DB; drafting composes the office persona/style (operational DB), so
/// <c>/draft</c> pins BOTH contexts. The version author is resolved from the operational directory.
/// Reads matters.read; drafting/accept/revert/save matters.write. Export renders markdown to docx/UDF.
/// </summary>
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(
    AgentDbContext db, KorkutDbContext office, IDraftDocumentService docs, DraftingService drafting, IUserLookup dir)
    : ControllerBase
{
    [HttpGet("/api/sessions/{sessionId:guid}/document")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> ForSession(Guid sessionId, [FromServices] ISessionService sessions, CancellationToken ct)
    {
        var data = await User.InTenantScopeAsync(db, async o =>
        {
            var conv = await sessions.GetAsync(o, sessionId, ct);
            if (conv is null)
            {
                return null;
            }

            var doc = await docs.GetOrCreateForSessionAsync(o, sessionId, conv.Title, ct);
            return await BuildAsync(o, doc.Id, docs, ct);
        }, ct);
        return data is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : Ok(data);
    }

    [HttpPost("{docId:guid}/draft")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Draft(Guid docId, [FromBody] DraftRequest body, CancellationToken ct)
    {
        var uid = User.UserId();
        try
        {
            var result = await User.InAgentAndOfficeScopeAsync(db, office,
                o => drafting.DraftAsync(o, uid, docId, body.Instruction!.Trim(), body.PetitionType, ct), ct);
            return result is null
                ? throw ApiException.NotFound("Belge bulunamadı.")
                : Ok(new PendingEditResponse(result.PendingId, result.ContentMd, result.Summary));
        }
        catch (ModelUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = ex.Message });
        }
    }

    [HttpPost("{docId:guid}/accept")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Accept(Guid docId, CancellationToken ct)
    {
        var author = await dir.EmailAsync(User.OfficeId(), User.UserId(), ct);
        var data = await User.InTenantScopeAsync(db, async o =>
            await docs.AcceptPendingAsync(o, docId, author, ct) ? await BuildAsync(o, docId, docs, ct) : null, ct);
        return data is null ? throw ApiException.NotFound("Kabul edilecek taslak bulunamadı.") : Ok(data);
    }

    [HttpPost("{docId:guid}/revert")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Revert(Guid docId, CancellationToken ct)
    {
        var data = await User.InTenantScopeAsync(db, async o =>
            await docs.RevertPendingAsync(o, docId, ct) ? await BuildAsync(o, docId, docs, ct) : null, ct);
        return data is null ? throw ApiException.NotFound("Belge bulunamadı.") : Ok(data);
    }

    [HttpPut("{docId:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Save(Guid docId, [FromBody] SaveDocumentRequest body, CancellationToken ct)
    {
        var author = await dir.EmailAsync(User.OfficeId(), User.UserId(), ct);
        var data = await User.InTenantScopeAsync(db, async o =>
            await docs.SaveAsync(o, docId, body.Title!.Trim(), body.ContentMd!, author, ct) ? await BuildAsync(o, docId, docs, ct) : null, ct);
        return data is null ? throw ApiException.NotFound("Belge bulunamadı.") : Ok(data);
    }

    [HttpGet("{docId:guid}/export")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Export(Guid docId, [FromQuery] string? format, CancellationToken ct)
    {
        var fmt = format ?? "docx";
        if (fmt is not ("docx" or "udf"))
        {
            throw ApiException.BadRequest("format 'docx' veya 'udf' olmalıdır.");
        }

        var doc = await User.InTenantScopeAsync(db, o => docs.GetAsync(o, docId, ct), ct);
        if (doc is null)
        {
            throw ApiException.NotFound("Belge bulunamadı.");
        }

        var exported = MarkdownDocumentExporter.Export(fmt, doc.Title, doc.ContentMd);
        return File(exported.Bytes, exported.ContentType, exported.FileName);
    }

    private static async Task<DocumentDataResponse> BuildAsync(Guid officeId, Guid docId, IDraftDocumentService docs, CancellationToken ct)
    {
        var doc = (await docs.GetAsync(officeId, docId, ct))!;
        var versions = (await docs.VersionsAsync(officeId, docId, ct))
            .Select(v => new DocVersionResponse(v.Id, v.Author, v.Note, v.CreatedAt)).ToList();
        var pending = doc.PendingContentMd is null
            ? null
            : new PendingEditResponse(doc.PendingId ?? doc.Id, doc.PendingContentMd, doc.PendingSummary);
        return new DocumentDataResponse(doc.Id, doc.Title, doc.ContentMd, doc.Status, versions, pending);
    }
}
