using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Agent;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="IDraftDocumentRepository"/>. Accepting a pending edit
/// (or a direct save) snapshots an immutable <c>document_versions</c> row. Tenant context is
/// set by the caller (RLS).
/// </summary>
public sealed class DraftDocumentRepository(Agent.AgentDbContext db) : IDraftDocumentRepository
{
    public async Task<GeneratedDocument> GetOrCreateForSessionAsync(Guid officeId, Guid conversationId, string title, DateTimeOffset now, CancellationToken ct = default)
    {
        var existing = await db.GeneratedDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.ConversationId == conversationId, ct);
        if (existing is not null)
        {
            return ToDomain(existing);
        }

        var doc = new GeneratedDocumentEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, ConversationId = conversationId,
            Title = title, ContentMd = string.Empty, Status = "draft", CreatedAt = now, UpdatedAt = now,
        };
        db.GeneratedDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return ToDomain(doc);
    }

    public async Task<GeneratedDocument?> GetAsync(Guid officeId, Guid docId, CancellationToken ct = default)
    {
        var d = await db.GeneratedDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);
        return d is null ? null : ToDomain(d);
    }

    public async Task<IReadOnlyList<DocumentVersion>> VersionsAsync(Guid officeId, Guid docId, CancellationToken ct = default)
        => await db.DocumentVersions.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.DocumentId == docId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new DocumentVersion(x.Id, x.DocumentId, x.ContentMd, x.Author, x.Note, x.CreatedAt))
            .ToListAsync(ct);

    public async Task<Guid?> StagePendingAsync(Guid officeId, Guid docId, string contentMd, string summary, DateTimeOffset now, CancellationToken ct = default)
    {
        var pendingId = UuidV7.NewId();
        var affected = await db.GeneratedDocuments.Where(x => x.OfficeId == officeId && x.Id == docId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PendingId, pendingId)
                .SetProperty(x => x.PendingContentMd, contentMd)
                .SetProperty(x => x.PendingSummary, summary)
                .SetProperty(x => x.UpdatedAt, now), ct);
        return affected > 0 ? pendingId : null;
    }

    public async Task<bool> AcceptPendingAsync(Guid officeId, Guid docId, string? author, DateTimeOffset now, CancellationToken ct = default)
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
        doc.UpdatedAt = now;
        db.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, DocumentId = docId, ContentMd = doc.ContentMd,
            Author = author, Note = "Taslak kabul edildi", CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RevertPendingAsync(Guid officeId, Guid docId, DateTimeOffset now, CancellationToken ct = default)
        => await db.GeneratedDocuments.Where(x => x.OfficeId == officeId && x.Id == docId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PendingId, (Guid?)null)
                .SetProperty(x => x.PendingContentMd, (string?)null)
                .SetProperty(x => x.PendingSummary, (string?)null)
                .SetProperty(x => x.UpdatedAt, now), ct) > 0;

    public async Task<bool> SaveAsync(Guid officeId, Guid docId, string title, string contentMd, string? author, DateTimeOffset now, CancellationToken ct = default)
    {
        var doc = await db.GeneratedDocuments.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == docId, ct);
        if (doc is null)
        {
            return false;
        }

        doc.Title = title;
        doc.ContentMd = contentMd;
        doc.UpdatedAt = now;
        db.DocumentVersions.Add(new DocumentVersionEntity
        {
            Id = UuidV7.NewId(), OfficeId = officeId, DocumentId = docId, ContentMd = contentMd,
            Author = author, Note = "Elle kaydedildi", CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static GeneratedDocument ToDomain(GeneratedDocumentEntity d) =>
        new(d.Id, d.OfficeId, d.ConversationId, d.Title, d.ContentMd, d.Status,
            d.PendingId, d.PendingContentMd, d.PendingSummary, d.CreatedAt, d.UpdatedAt);
}
