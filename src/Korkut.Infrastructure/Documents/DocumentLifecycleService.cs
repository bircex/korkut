using Korkut.Application.Documents;
using Korkut.Application.Errors;
using Korkut.Domain.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Application.Documents.Storage;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Documents;

/// <summary>
/// The document delete/tombstone/retention state machine (KDM-027). A tombstone
/// purges the bytes and extraction artifacts but keeps the metadata row as an
/// auditable marker. Legal hold is an absolute veto over both explicit deletes and
/// retention-driven purges.
/// </summary>
public sealed class DocumentLifecycleService(KorkutDbContext db, IObjectStoreService storeService, IClock clock)
    : IDocumentLifecycleService
{
    /// <summary>
    /// Tombstones a document: removes the object bytes and its extraction artifacts,
    /// then marks the row <c>tombstoned</c>. Refuses if the document is under legal hold.
    /// Idempotent — tombstoning an already-tombstoned document is a no-op.
    /// </summary>
    public async Task TombstoneAsync(Guid officeId, Guid documentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == documentId, ct)
            ?? throw new NotFoundException("Belge bulunamadı.");

        if (doc.LegalHold)
        {
            throw new ConflictException("Belge yasal muhafaza altında, silinemez.");
        }

        if (doc.IsTombstoned)
        {
            return;
        }

        await PurgeArtifactsAsync(officeId, documentId, doc.Bucket, doc.ObjectKey, ct);

        doc.Tombstone(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        // A tombstoned/revoked document must leave the search index (KDM-028).
        await new IndexOutboxService(db, clock).EnqueueDeindexAsync(officeId, documentId, doc.Version, ct);
    }

    /// <summary>
    /// Places or lifts a legal hold. A held document is exempt from delete and purge.
    /// </summary>
    public async Task SetLegalHoldAsync(Guid officeId, Guid documentId, bool held, CancellationToken ct = default)
    {
        var affected = await db.Documents
            .Where(x => x.OfficeId == officeId && x.Id == documentId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LegalHold, held), ct);
        if (affected == 0)
        {
            throw new NotFoundException("Belge bulunamadı.");
        }
    }

    /// <summary>
    /// Tombstones every document whose retention window has elapsed and that is not
    /// under legal hold. Returns the count tombstoned. Held or already-tombstoned rows
    /// are left untouched.
    /// </summary>
    public async Task<int> PurgeExpiredAsync(Guid officeId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var expired = await db.Documents
            .Where(x => x.OfficeId == officeId
                && x.RetentionUntil != null
                && x.RetentionUntil < now
                && !x.LegalHold
                && x.Status != DocumentStatus.Tombstoned)
            .Select(x => new { x.Id, x.Bucket, x.ObjectKey, x.Version })
            .ToListAsync(ct);

        foreach (var d in expired)
        {
            await PurgeArtifactsAsync(officeId, d.Id, d.Bucket, d.ObjectKey, ct);
        }

        if (expired.Count > 0)
        {
            var ids = expired.Select(x => x.Id).ToArray();
            await db.Documents
                .Where(x => x.OfficeId == officeId && ids.Contains(x.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.Status, DocumentStatus.Tombstoned).SetProperty(x => x.TombstonedAt, now),
                    ct);

            var index = new IndexOutboxService(db, clock);
            foreach (var d in expired)
            {
                await index.EnqueueDeindexAsync(officeId, d.Id, d.Version, ct);
            }
        }

        return expired.Count;
    }

    private async Task PurgeArtifactsAsync(Guid officeId, Guid documentId, string bucket, string objectKey, CancellationToken ct)
    {
        // Best-effort object removal (bytes gone); metadata row is retained as a tombstone.
        await storeService.RemoveAsync(bucket, objectKey, ct);
        await db.DocumentExtractions
            .Where(x => x.OfficeId == officeId && x.DocumentId == documentId)
            .ExecuteDeleteAsync(ct);
    }
}
