using Korkut.Jobs.Entities;
using Korkut.Application.Documents;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Enqueues search-index / de-index events onto the transactional outbox (KDM-028).
/// Idempotency keys make re-enqueue a no-op (projection idempotency): one index event
/// per (document, version), one de-index event per document. No search engine is
/// contacted here — only the contract is produced.
/// </summary>
public sealed class IndexOutboxService(KorkutDbContext db, IClock clock)
{
    public Task<bool> EnqueueIndexAsync(Guid officeId, Guid documentId, int version, CancellationToken ct = default)
        => EnqueueAsync(
            officeId,
            DocumentIndexEventTypes.Index,
            $"index:{documentId}:{version}",
            new DocumentIndexEvent(officeId, documentId, version, "index"),
            ct);

    public Task<bool> EnqueueDeindexAsync(Guid officeId, Guid documentId, int version, CancellationToken ct = default)
        => EnqueueAsync(
            officeId,
            DocumentIndexEventTypes.Deindex,
            $"deindex:{documentId}",
            new DocumentIndexEvent(officeId, documentId, version, "deindex"),
            ct);

    private async Task<bool> EnqueueAsync(Guid officeId, string type, string key, DocumentIndexEvent evt, CancellationToken ct)
    {
        var exists = await db.Outbox.AsNoTracking()
            .AnyAsync(x => x.OfficeId == officeId && x.IdempotencyKey == key, ct);
        if (exists)
        {
            return false;
        }

        var now = clock.UtcNow;
        // Provenance-preserving payload (tenant + document + version).
        var payload =
            $"{{\"office_id\":\"{evt.OfficeId}\",\"document_id\":\"{evt.DocumentId}\",\"version\":{evt.DocumentVersion},\"op\":\"{evt.Op}\"}}";
        db.Outbox.Add(new OutboxMessage(officeId, type, payload, key, now, now));
        await db.SaveChangesAsync(ct);
        return true;
    }
}
