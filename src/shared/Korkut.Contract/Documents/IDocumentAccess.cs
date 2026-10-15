namespace Korkut.Contract.Documents;

/// <summary>
/// Which documents the caller may see (cross-module contract): their own uploads, those linked to
/// a matter in their scope, or all of them with every matter in scope. Call it inside the tenant scope.
/// </summary>
public interface IDocumentAccess
{
    /// <summary>The visible documents, narrowed to one matter's when <paramref name="matterId"/> is
    /// given. Null means no restriction (every document, no matter narrowing).</summary>
    Task<IReadOnlySet<Guid>?> VisibleDocumentIdsAsync(Guid officeId, Guid? matterId = null, CancellationToken ct = default);
}
