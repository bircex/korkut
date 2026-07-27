namespace Korkut.Core;

/// <summary>
/// The base persistence port every aggregate repository derives from. Tenancy is mandatory,
/// not optional: <paramref name="officeId"/> is always the first argument, so it is impossible
/// to express a cross-tenant query through this contract.
/// </summary>
public interface IRepository<TEntity>
    where TEntity : Entity, ITenantOwned, IAggregateRoot
{
    /// <summary>The entity with this id inside the office, or null.</summary>
    Task<TEntity?> FindAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Tracked load for mutation; <see cref="FindAsync"/> is the no-tracking read path.</summary>
    Task<TEntity?> FindForUpdateAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TEntity>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task AddAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>Hard-removes the row. Soft-deletable aggregates use <see cref="ISoftDeleteRepository{TEntity}"/>.</summary>
    Task<bool> RemoveAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
