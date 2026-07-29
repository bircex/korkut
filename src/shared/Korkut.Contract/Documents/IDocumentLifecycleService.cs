namespace Korkut.Contract.Documents;

/// <summary>
/// Deletion, legal hold and retention. Deleting means tombstoning: the bytes and extraction
/// artifacts go, the metadata row stays as an auditable marker. Legal hold vetoes both.
/// </summary>
public interface IDocumentLifecycleService
{
    Task TombstoneAsync(Guid officeId, Guid documentId, CancellationToken ct = default);

    Task SetLegalHoldAsync(Guid officeId, Guid documentId, bool held, CancellationToken ct = default);

    /// <summary>Tombstones everything past its retention window that is not held; returns how many.</summary>
    Task<int> PurgeExpiredAsync(Guid officeId, CancellationToken ct = default);
}
