using Korkut.Core;

namespace Korkut.Core;

/// <summary>
/// The base port for aggregates that are retired by stamping a deletion time rather than
/// removing the row. Reads through this repository exclude soft-deleted rows by default.
/// </summary>
public interface ISoftDeleteRepository<TEntity> : IRepository<TEntity>
    where TEntity : Entity, ITenantOwned, IAggregateRoot, ISoftDeletable
{
    Task<bool> SoftDeleteAsync(Guid officeId, Guid id, DateTimeOffset deletedAt, CancellationToken ct = default);
}
