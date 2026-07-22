using Korkut.Agent.Contracts.Documents;
using Korkut.Agent.Repositories.Core;
using Korkut.Agent.Services.Core;
using Korkut.Agent.Services.Core.Chat;
using Korkut.Agent.Services.Core.Documents;
using Korkut.Agent.Services.Core.Sessions;
using Korkut.Application.Abstractions.Directory;
using Korkut.Infrastructure.Documents;
using Korkut.Web.Errors;

namespace Korkut.Agent.Services.Implementations.Documents;

/// <summary>
/// AI-drafted-document use-cases the controller forwards to: runs the domain services inside a
/// tenant-scoped unit of work (<see cref="IAgentUnitOfWork"/> — the only DbContext holder). Drafting
/// composes the office persona so it pins BOTH databases; the rest touch the Agent DB. Not-found /
/// bad-format are thrown <see cref="ApiException"/>s the shared handler maps.
/// </summary>
public sealed class DocumentAppService(
    IAgentUnitOfWork uow, ICurrentUser current,
    ISessionService sessions, IDraftDocumentService docs, IDraftingService drafting, IUserLookup dir) : IDocumentAppService
{
    public async Task<DocumentDataResponse> ForSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var data = await uow.InScopeAsync(office, async () =>
        {
            var conv = await sessions.GetAsync(office, sessionId, ct);
            if (conv is null)
            {
                return null;
            }

            var doc = await docs.GetOrCreateForSessionAsync(office, sessionId, conv.Title, ct);
            return await BuildAsync(office, doc.Id, ct);
        }, ct);
        return data ?? throw ApiException.NotFound("Görüşme bulunamadı.");
    }

    public async Task<PendingEditResponse> DraftAsync(Guid docId, string instruction, string? petitionType, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var result = await uow.InDualScopeAsync(office,
            () => drafting.DraftAsync(office, current.UserId, docId, instruction.Trim(), petitionType, ct), ct);
        return result is null
            ? throw ApiException.NotFound("Belge bulunamadı.")
            : new PendingEditResponse(result.PendingId, result.ContentMd, result.Summary);
    }

    public async Task<DocumentDataResponse> AcceptAsync(Guid docId, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var author = await dir.EmailAsync(office, current.UserId, ct);
        var data = await uow.InScopeAsync(office,
            async () => await docs.AcceptPendingAsync(office, docId, author, ct) ? await BuildAsync(office, docId, ct) : null, ct);
        return data ?? throw ApiException.NotFound("Kabul edilecek taslak bulunamadı.");
    }

    public async Task<DocumentDataResponse> RevertAsync(Guid docId, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var data = await uow.InScopeAsync(office,
            async () => await docs.RevertPendingAsync(office, docId, ct) ? await BuildAsync(office, docId, ct) : null, ct);
        return data ?? throw ApiException.NotFound("Belge bulunamadı.");
    }

    public async Task<DocumentDataResponse> SaveAsync(Guid docId, string title, string contentMd, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var author = await dir.EmailAsync(office, current.UserId, ct);
        var data = await uow.InScopeAsync(office,
            async () => await docs.SaveAsync(office, docId, title.Trim(), contentMd, author, ct) ? await BuildAsync(office, docId, ct) : null, ct);
        return data ?? throw ApiException.NotFound("Belge bulunamadı.");
    }

    public async Task<ExportedDocument> ExportAsync(Guid docId, string? format, CancellationToken ct = default)
    {
        var fmt = format ?? "docx";
        if (fmt is not ("docx" or "udf"))
        {
            throw ApiException.BadRequest("format 'docx' veya 'udf' olmalıdır.");
        }

        var office = current.OfficeId;
        var doc = await uow.InScopeAsync(office, () => docs.GetAsync(office, docId, ct), ct);
        if (doc is null)
        {
            throw ApiException.NotFound("Belge bulunamadı.");
        }

        return MarkdownDocumentExporter.Export(fmt, doc.Title, doc.ContentMd);
    }

    private async Task<DocumentDataResponse> BuildAsync(Guid officeId, Guid docId, CancellationToken ct)
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
