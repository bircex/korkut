using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Agent.Repositories.Core;

/// <summary>
/// The base repository contract shared by every Agent repository. Tenancy is MANDATORY: the caller's
/// office is a required argument on every operation and is enforced on top of Postgres RLS, so a
/// tenant-blind query is impossible by construction. This carries the generic entity CRUD; the
/// concrete repository interfaces add their bespoke, domain-returning queries.
/// </summary>
public interface IRepository<TEntity> where TEntity : class, ITenantOwned, IIdentifiable
{
    /// <summary>The office-scoped entity with this id, or null.</summary>
    Task<TEntity?> FindAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Insert a new row (its <c>OfficeId</c> must already be set by the caller) and save.</summary>
    Task InsertAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>Delete the office-scoped row with this id; true if a row was removed.</summary>
    Task<bool> RemoveAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Flush pending changes.</summary>
    Task<int> SaveAsync(CancellationToken ct = default);
}
