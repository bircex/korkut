namespace Korkut.Contract.Documents;

/// <summary>
/// Files documents against clients, matters and parties. Both sides must live in the same
/// office, every change is audited, and re-filing an existing pair updates it instead of
/// duplicating it.
/// </summary>
public interface IDocumentRelationService
{
    Task<Guid> LinkAsync(
        Guid officeId, Guid actorId, Guid documentId, string targetType, Guid targetId, bool isPrimary,
        CancellationToken ct = default);

    Task UnlinkAsync(Guid officeId, Guid actorId, Guid documentId, string targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>The documents reachable through a target — the permission-inheritance query.</summary>
    Task<IReadOnlyList<Guid>> DocumentsForTargetAsync(Guid officeId, string targetType, Guid targetId, CancellationToken ct = default);
}
