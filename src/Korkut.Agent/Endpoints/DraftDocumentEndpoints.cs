using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Agent.Tenancy;
using Korkut.Application.Abstractions.Directory;
using Korkut.Application.Ai;
using Korkut.Application.Features.Agent;
using Korkut.Application.Security;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Errors;
using Korkut.Web.Tenancy;

namespace Korkut.Agent.Endpoints;

public sealed record DocVersionResponse(Guid Id, string? Author, string? Note, DateTimeOffset CreatedAt);

public sealed record PendingEditResponse(Guid Id, string ContentMd, string? Summary);

public sealed record DocumentDataResponse(
    Guid Id, string Title, string ContentMd, string Status, IReadOnlyList<DocVersionResponse> Versions, PendingEditResponse? Pending);

public sealed record DraftRequest
{
    [Required, MaxLength(4000)]
    public string? Instruction { get; init; }

    [MaxLength(100)]
    public string? PetitionType { get; init; }
}

public sealed record SaveDocumentRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }

    [Required]
    public string? ContentMd { get; init; }
}

/// <summary>
/// AI-drafted documents (the KDM document editor), served by the Agent host. Authenticated,
/// tenant-scoped (RLS). A document is created per session; the assistant stages pending edits
/// the user accepts or reverts, plus direct save and export. Document state lives in the Agent
/// DB; drafting composes the office persona/style (operational DB), so <c>/draft</c> pins BOTH
/// contexts. The version author is resolved from the operational directory. Reads matters.read;
/// drafting/accept/revert/save matters.write. Export renders the current markdown to docx/UDF.
/// </summary>
public static class DraftDocumentEndpoints
{
    public static IEndpointRouteBuilder MapDraftDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sessions/{sessionId:guid}/document", async (Guid sessionId, ClaimsPrincipal user, AgentDbContext db, ISessionService sessions, IDraftDocumentService docs, CancellationToken ct) =>
            {
                var data = await user.InTenantScopeAsync(db, async o =>
                {
                    var conv = await sessions.GetAsync(o, sessionId, ct);
                    if (conv is null)
                    {
                        return null;
                    }

                    var doc = await docs.GetOrCreateForSessionAsync(o, sessionId, conv.Title, ct);
                    return await BuildAsync(o, doc.Id, docs, ct);
                }, ct);
                return data is null ? throw ApiException.NotFound("Görüşme bulunamadı.") : Results.Ok(data);
            })
            .RequireAuthorization(Permissions.MattersRead);

        var documents = app.MapGroup("/api/documents");

        documents.MapPost("/{docId:guid}/draft", async (Guid docId, DraftRequest body, ClaimsPrincipal user, AgentDbContext db, KorkutDbContext office, DraftingService drafting, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var uid = user.UserId();
                try
                {
                    var result = await user.InAgentAndOfficeScopeAsync(db, office, o => drafting.DraftAsync(o, uid, docId, body.Instruction!.Trim(), body.PetitionType, ct), ct);
                    return result is null
                        ? throw ApiException.NotFound("Belge bulunamadı.")
                        : Results.Ok(new PendingEditResponse(result.PendingId, result.ContentMd, result.Summary));
                }
                catch (ModelUnavailableException ex)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireAuthorization(Permissions.MattersWrite);

        documents.MapPost("/{docId:guid}/accept", async (Guid docId, ClaimsPrincipal user, AgentDbContext db, IDraftDocumentService docs, IUserLookup dir, CancellationToken ct) =>
            {
                var author = await dir.EmailAsync(user.OfficeId(), user.UserId(), ct);
                var data = await user.InTenantScopeAsync(db, async o =>
                    await docs.AcceptPendingAsync(o, docId, author, ct) ? await BuildAsync(o, docId, docs, ct) : null, ct);
                return data is null ? throw ApiException.NotFound("Kabul edilecek taslak bulunamadı.") : Results.Ok(data);
            })
            .RequireAuthorization(Permissions.MattersWrite);

        documents.MapPost("/{docId:guid}/revert", async (Guid docId, ClaimsPrincipal user, AgentDbContext db, IDraftDocumentService docs, CancellationToken ct) =>
            {
                var data = await user.InTenantScopeAsync(db, async o =>
                    await docs.RevertPendingAsync(o, docId, ct) ? await BuildAsync(o, docId, docs, ct) : null, ct);
                return data is null ? throw ApiException.NotFound("Belge bulunamadı.") : Results.Ok(data);
            })
            .RequireAuthorization(Permissions.MattersWrite);

        documents.MapPut("/{docId:guid}", async (Guid docId, SaveDocumentRequest body, ClaimsPrincipal user, AgentDbContext db, IDraftDocumentService docs, IUserLookup dir, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var author = await dir.EmailAsync(user.OfficeId(), user.UserId(), ct);
                var data = await user.InTenantScopeAsync(db, async o =>
                    await docs.SaveAsync(o, docId, body.Title!.Trim(), body.ContentMd!, author, ct) ? await BuildAsync(o, docId, docs, ct) : null, ct);
                return data is null ? throw ApiException.NotFound("Belge bulunamadı.") : Results.Ok(data);
            })
            .RequireAuthorization(Permissions.MattersWrite);

        documents.MapGet("/{docId:guid}/export", async (Guid docId, string? format, ClaimsPrincipal user, AgentDbContext db, IDraftDocumentService docs, CancellationToken ct) =>
            {
                var fmt = format ?? "docx";
                if (fmt is not ("docx" or "udf"))
                {
                    throw ApiException.BadRequest("format 'docx' veya 'udf' olmalıdır.");
                }

                var doc = await user.InTenantScopeAsync(db, o => docs.GetAsync(o, docId, ct), ct);
                if (doc is null)
                {
                    throw ApiException.NotFound("Belge bulunamadı.");
                }

                var exported = MarkdownDocumentExporter.Export(fmt, doc.Title, doc.ContentMd);
                return Results.File(exported.Bytes, exported.ContentType, exported.FileName);
            })
            .RequireAuthorization(Permissions.MattersRead);

        return app;
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
