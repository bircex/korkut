using Korkut.Agent.Application.Abstractions;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Agent.Application.Contracts.Documents;
using Korkut.Application.Abstractions.Directory;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Web.Errors;

namespace Korkut.Agent.Application.Documents;

/// <summary>
/// AI-drafted-document use-cases as the controller sees them: owns the tenant scope(s), the domain
/// calls, author resolution, and response-DTO mapping. Drafting composes the office persona so it
/// pins BOTH databases; the rest touch the Agent DB. Not-found / bad-format are thrown
/// <see cref="ApiException"/>s the shared handler maps.
/// </summary>
public sealed class DocumentAppService(
    AgentDbContext agent, KorkutDbContext officeDb, ICurrentUser current,
    ISessionService sessions, IDraftDocumentService docs, DraftingService drafting, IUserLookup dir)
{
    public async Task<DocumentDataResponse> ForSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var data = await ScopedAsync(async o =>
        {
            var conv = await sessions.GetAsync(o, sessionId, ct);
            if (conv is null)
            {
                return null;
            }

            var doc = await docs.GetOrCreateForSessionAsync(o, sessionId, conv.Title, ct);
            return await BuildAsync(o, doc.Id, ct);
        }, ct);
        return data ?? throw ApiException.NotFound("Görüşme bulunamadı.");
    }

    public async Task<PendingEditResponse> DraftAsync(Guid docId, string instruction, string? petitionType, CancellationToken ct = default)
    {
        var result = await DualScopedAsync(o => drafting.DraftAsync(o, current.UserId, docId, instruction.Trim(), petitionType, ct), ct);
        return result is null
            ? throw ApiException.NotFound("Belge bulunamadı.")
            : new PendingEditResponse(result.PendingId, result.ContentMd, result.Summary);
    }

    public async Task<DocumentDataResponse> AcceptAsync(Guid docId, CancellationToken ct = default)
    {
        var author = await dir.EmailAsync(current.OfficeId, current.UserId, ct);
        var data = await ScopedAsync(async o => await docs.AcceptPendingAsync(o, docId, author, ct) ? await BuildAsync(o, docId, ct) : null, ct);
        return data ?? throw ApiException.NotFound("Kabul edilecek taslak bulunamadı.");
    }

    public async Task<DocumentDataResponse> RevertAsync(Guid docId, CancellationToken ct = default)
    {
        var data = await ScopedAsync(async o => await docs.RevertPendingAsync(o, docId, ct) ? await BuildAsync(o, docId, ct) : null, ct);
        return data ?? throw ApiException.NotFound("Belge bulunamadı.");
    }

    public async Task<DocumentDataResponse> SaveAsync(Guid docId, string title, string contentMd, CancellationToken ct = default)
    {
        var author = await dir.EmailAsync(current.OfficeId, current.UserId, ct);
        var data = await ScopedAsync(async o => await docs.SaveAsync(o, docId, title.Trim(), contentMd, author, ct) ? await BuildAsync(o, docId, ct) : null, ct);
        return data ?? throw ApiException.NotFound("Belge bulunamadı.");
    }

    public async Task<ExportedDocument> ExportAsync(Guid docId, string? format, CancellationToken ct = default)
    {
        var fmt = format ?? "docx";
        if (fmt is not ("docx" or "udf"))
        {
            throw ApiException.BadRequest("format 'docx' veya 'udf' olmalıdır.");
        }

        var doc = await ScopedAsync(o => docs.GetAsync(o, docId, ct), ct);
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

    private async Task<T> ScopedAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = current.OfficeId;
        await using var scope = await agent.BeginTenantScopeAsync(office, ct);
        var result = await work(office);
        await scope.CommitAsync(ct);
        return result;
    }

    private async Task<T> DualScopedAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = current.OfficeId;
        await using var agentScope = await agent.BeginTenantScopeAsync(office, ct);
        await using var officeScope = await officeDb.BeginTenantScopeAsync(office, ct);
        var result = await work(office);
        await officeScope.CommitAsync(ct);
        await agentScope.CommitAsync(ct);
        return result;
    }
}
