using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Agent.Drafting;
using Korkut.Agent.Drafting.Entities;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Agent.Persistence;

/// <summary>
/// EF/Postgres adapter for <see cref="IDraftDocumentRepository"/>. Accepting a pending edit
/// (or a direct save) snapshots an immutable <c>document_versions</c> row. Tenant context is
/// set by the caller (RLS).
/// </summary>
public sealed class DraftDocumentRepository(AgentDbContext db) : EfRepository<GeneratedDocument>(db), IDraftDocumentRepository
{
    public async Task<GeneratedDocument> GetOrCreateForSessionAsync(Guid officeId, Guid conversationId, string title, DateTimeOffset now, CancellationToken ct = default)
    {
        var existing = await Db.GeneratedDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.ConversationId == conversationId, ct);
        if (existing is not null)
        {
            return existing;
        }

        var doc = new GeneratedDocument(officeId, conversationId, title, now);
        Db.GeneratedDocuments.Add(doc);
        await Db.SaveChangesAsync(ct);
        return doc;
    }

    public Task<GeneratedDocument?> GetAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => Db.GeneratedDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);

    public async Task<IReadOnlyList<DocumentVersion>> VersionsAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => await Db.DocumentVersions.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.DocumentId == docId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<Guid?> StagePendingAsync(Guid officeId, Guid docId, string contentMd, string summary, DateTimeOffset now, CancellationToken ct = default)
    {
        var pendingId = UuidV7.NewId();
        var affected = await Db.GeneratedDocuments.Where(x => x.OfficeId == officeId && x.Id == docId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PendingId, pendingId)
                .SetProperty(x => x.PendingContentMd, contentMd)
                .SetProperty(x => x.PendingSummary, summary)
                .SetProperty(x => x.UpdatedAt, now), ct);
        return affected > 0 ? pendingId : null;
    }

    public async Task<bool> AcceptPendingAsync(Guid officeId, Guid docId, string? author, DateTimeOffset now, CancellationToken ct = default)
    {
        var doc = await Db.GeneratedDocuments.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);
        if (doc is null || !doc.AcceptPending(now))
        {
            return false;
        }

        Db.DocumentVersions.Add(new DocumentVersion(officeId, docId, doc.ContentMd, author, "Taslak kabul edildi", now));
        await Db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RevertPendingAsync(Guid officeId, Guid docId, DateTimeOffset now, CancellationToken ct = default)
        => await Db.GeneratedDocuments.Where(x => x.OfficeId == officeId && x.Id == docId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PendingId, (Guid?)null)
                .SetProperty(x => x.PendingContentMd, (string?)null)
                .SetProperty(x => x.PendingSummary, (string?)null)
                .SetProperty(x => x.UpdatedAt, now), ct) > 0;

    public async Task<bool> SaveAsync(Guid officeId, Guid docId, string title, string contentMd, string? author, DateTimeOffset now, CancellationToken ct = default)
    {
        var doc = await Db.GeneratedDocuments.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);
        if (doc is null)
        {
            return false;
        }

        doc.Save(title, contentMd, now);
        Db.DocumentVersions.Add(new DocumentVersion(officeId, docId, contentMd, author, "Elle kaydedildi", now));
        await Db.SaveChangesAsync(ct);
        return true;
    }
}
