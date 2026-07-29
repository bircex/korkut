namespace Korkut.Contract.Documents;

/// <summary>
/// Enqueues search-index / de-index events onto the transactional outbox. The document pipeline
/// depends on this port; the impl (which writes the Jobs outbox table) lives in Infrastructure,
/// so the Documents module never references Jobs. Idempotency keys make re-enqueue a no-op.
/// </summary>
public interface IDocumentIndexPublisher
{
    Task<bool> EnqueueIndexAsync(Guid officeId, Guid documentId, int version, CancellationToken ct = default);

    Task<bool> EnqueueDeindexAsync(Guid officeId, Guid documentId, int version, CancellationToken ct = default);
}
