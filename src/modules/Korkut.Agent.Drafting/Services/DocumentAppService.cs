using Korkut.Agent.Chat.Services;
using Korkut.Agent.Drafting.Entities;
using Korkut.Agent.Drafting.Repositories;
using Korkut.Agent.Drafting.Abstractions;
using Korkut.Contract.Agent.Drafting;
using Korkut.Agent.Chat.Abstractions;
using Korkut.Contract.Agent.Chat;
using Korkut.Contract.Agent;
using Korkut.Contract.Export;
using Korkut.Core;
using ICurrentUser = Korkut.Contract.Agent.ICurrentUser;

namespace Korkut.Agent.Drafting.Services;

/// <summary>
/// AI-drafted-document use-cases the controller forwards to: runs the domain services inside a
/// tenant-scoped unit of work (<see cref="IAgentUnitOfWork"/> — the only DbContext holder). Drafting
/// composes the office persona so it pins BOTH databases; the rest touch the Agent DB. Not-found /
/// bad-format are thrown <see cref="NotFoundException"/> / <see cref="BadRequestException"/> the shared handler maps.
/// A draft belongs to its conversation: whoever may see the conversation may see and work on the
/// draft (<see cref="ConversationAccess"/>); anyone else gets not found.
/// </summary>
internal sealed class DocumentAppService(
    IAgentUnitOfWork uow, ICurrentUser current,
    ISessionService sessions, IDraftDocumentService docs, IDraftingService drafting,
    IUserLookupService dir, IDocumentExporter exporter, ConversationAccess access) : IDocumentAppService
{
    public async Task<IReadOnlyList<DocBriefResponse>> ListAsync(CancellationToken ct = default)
    {
        var office = current.OfficeId;
        var rows = await uow.InDualScopeAsync(office, async () =>
        {
            var visible = new List<GeneratedDocument>();
            foreach (var d in await docs.ListAsync(office, ct))
            {
                if (await access.CanSeeAsync(office, d.ConversationId, ct))
                {
                    visible.Add(d);
                }
            }

            return visible;
        }, ct);
        return rows
            .Select(d => new DocBriefResponse(d.Id, d.ConversationId, d.Title, KindOf(d.Title), d.UpdatedAt))
            .ToList();
    }

    /// <summary>The draft's conversation must be the caller's to see; otherwise the draft does not exist for them.</summary>
    private async Task EnsureVisibleAsync(Guid office, Guid docId, CancellationToken ct)
    {
        var visible = await uow.InDualScopeAsync(office, async () =>
            await docs.GetAsync(office, docId, ct) is { } doc && await access.CanSeeAsync(office, doc.ConversationId, ct), ct);
        if (!visible)
        {
            throw new NotFoundException("Belge bulunamadı.");
        }
    }

    /// <summary>
    /// The library groups drafts as "sözleşme" vs everything else (dilekçe). The store keeps no
    /// kind column, so classify by title until drafting records an explicit type.
    /// </summary>
    private static string? KindOf(string title)
    {
        var t = title.ToLowerInvariant();
        return t.Contains("sözleşme") || t.Contains("protokol") ? "sozlesme" : null;
    }

    public async Task<DocumentDataResponse> ForSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var office = current.OfficeId;

        // DUAL scope: the conversation lives on the operational DB, the draft document on the
        // Agent DB — an agent-only scope leaves the operational connection without its RLS GUC
        // (22P02 "invalid input syntax for type uuid" at runtime).
        var data = await uow.InDualScopeAsync(office, async () =>
        {
            var conv = await sessions.GetAsync(office, sessionId, ct);

            if (conv is null || !await access.CanSeeAsync(office, conv.MatterId, conv.CreatedBy, ct))
            {
                return null;
            }

            var doc = await docs.GetOrCreateForSessionAsync(office, sessionId, conv.Title, ct);
            
            return await BuildAsync(office, doc.Id, ct);
        }, ct);
        
        return data ?? throw new NotFoundException("Görüşme bulunamadı.");
    }

    public async Task<PendingEditResponse> DraftAsync(Guid docId, string instruction, string? petitionType, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        await EnsureVisibleAsync(office, docId, ct);
        var result = await uow.InDualScopeAsync(office,
            () => drafting.DraftAsync(office, current.UserId, docId, instruction.Trim(), petitionType, ct), ct);
        return result is null
            ? throw new NotFoundException("Belge bulunamadı.")
            : new PendingEditResponse(result.PendingId, result.ContentMd, result.Summary);
    }

    public async Task<DocumentDataResponse> AcceptAsync(Guid docId, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        await EnsureVisibleAsync(office, docId, ct);
        var author = await dir.EmailAsync(office, current.UserId, ct);
        var data = await uow.InScopeAsync(office,
            async () => await docs.AcceptPendingAsync(office, docId, author, ct) ? await BuildAsync(office, docId, ct) : null, ct);
        return data ?? throw new NotFoundException("Kabul edilecek taslak bulunamadı.");
    }

    public async Task<DocumentDataResponse> RevertAsync(Guid docId, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        await EnsureVisibleAsync(office, docId, ct);
        var data = await uow.InScopeAsync(office,
            async () => await docs.RevertPendingAsync(office, docId, ct) ? await BuildAsync(office, docId, ct) : null, ct);
        return data ?? throw new NotFoundException("Belge bulunamadı.");
    }

    public async Task<DocumentDataResponse> SaveAsync(Guid docId, string title, string contentMd, CancellationToken ct = default)
    {
        var office = current.OfficeId;
        await EnsureVisibleAsync(office, docId, ct);
        var author = await dir.EmailAsync(office, current.UserId, ct);
        var data = await uow.InScopeAsync(office,
            async () => await docs.SaveAsync(office, docId, title.Trim(), contentMd, author, ct) ? await BuildAsync(office, docId, ct) : null, ct);
        return data ?? throw new NotFoundException("Belge bulunamadı.");
    }

    public async Task<ExportedDocument> ExportAsync(Guid docId, string? format, CancellationToken ct = default)
    {
        var fmt = format ?? ExportFormat.Docx;
        if (!ExportFormat.IsSupported(fmt))
        {
            throw new BadRequestException("format 'docx' veya 'udf' olmalıdır.");
        }

        var office = current.OfficeId;
        await EnsureVisibleAsync(office, docId, ct);
        var doc = await uow.InScopeAsync(office, () => docs.GetAsync(office, docId, ct), ct);
        
        if (doc is null)
        {
            throw new NotFoundException("Belge bulunamadı.");
        }

        return exporter.Export(fmt, doc.Title, doc.ContentMd);
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
