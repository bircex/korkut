using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// AI-drafted documents tied to a conversation: get-or-create per session, stage/accept/
/// revert a pending edit, direct save, and version history. Tenant context is set by the
/// caller (RLS). Accepting a pending edit (or a save) snapshots an immutable version.
/// </summary>
public sealed class DraftDocumentStore(KorkutDbContext db, IClock clock)
{
    /// <summary>The generated document for a conversation, creating an empty one on first access.</summary>
    public async Task<GeneratedDocumentEntity> GetOrCreateForSessionAsync(Guid officeId, Guid conversationId, string title, CancellationToken ct = default)
    {
        var doc = await db.GeneratedDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.ConversationId == conversationId, ct);
        if (doc is not null)
        {
            return doc;
        }

        var now = clock.UtcNow;
        doc = new GeneratedDocumentEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, ConversationId = conversationId,
            Title = title, ContentMd = string.Empty, Status = "draft", CreatedAt = now, UpdatedAt = now,
        };
        db.GeneratedDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public Task<GeneratedDocumentEntity?> GetAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => db.GeneratedDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);

    public Task<List<DocumentVersionEntity>> VersionsAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => db.DocumentVersions.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.DocumentId == docId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    /// <summary>Stage a pending edit (a proposed new content + summary). Returns the pending id, or null if missing.</summary>
    public async Task<Guid?> StagePendingAsync(Guid officeId, Guid docId, string contentMd, string summary, CancellationToken ct = default)
    {
        var pendingId = UuidV7.NewId();
        var affected = await db.GeneratedDocuments.Where(x => x.OfficeId == officeId && x.Id == docId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PendingId, pendingId)
                .SetProperty(x => x.PendingContentMd, contentMd)
                .SetProperty(x => x.PendingSummary, summary)
                .SetProperty(x => x.UpdatedAt, clock.UtcNow), ct);
        return affected > 0 ? pendingId : null;
    }

    /// <summary>Apply the pending edit → current content + a new version, then clear it. Returns false if missing/none.</summary>
    public async Task<bool> AcceptPendingAsync(Guid officeId, Guid docId, string? author, CancellationToken ct = default)
    {
        var doc = await db.GeneratedDocuments.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);
        if (doc is null || doc.PendingContentMd is null)
        {
            return false;
        }

        doc.ContentMd = doc.PendingContentMd;
        doc.PendingId = null;
        doc.PendingContentMd = null;
        doc.PendingSummary = null;
        doc.UpdatedAt = clock.UtcNow;
        db.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, DocumentId = docId, ContentMd = doc.ContentMd,
            Author = author, Note = "Taslak kabul edildi", CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Discard the pending edit. Returns false if the document is missing.</summary>
    public async Task<bool> RevertPendingAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => await db.GeneratedDocuments.Where(x => x.OfficeId == officeId && x.Id == docId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PendingId, (Guid?)null)
                .SetProperty(x => x.PendingContentMd, (string?)null)
                .SetProperty(x => x.PendingSummary, (string?)null)
                .SetProperty(x => x.UpdatedAt, clock.UtcNow), ct) > 0;

    /// <summary>Directly save title + content and snapshot a version. Returns false if missing.</summary>
    public async Task<bool> SaveAsync(Guid officeId, Guid docId, string title, string contentMd, string? author, CancellationToken ct = default)
    {
        var doc = await db.GeneratedDocuments.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);
        if (doc is null)
        {
            return false;
        }

        doc.Title = title;
        doc.ContentMd = contentMd;
        doc.UpdatedAt = clock.UtcNow;
        db.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, DocumentId = docId, ContentMd = contentMd,
            Author = author, Note = "Elle kaydedildi", CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
