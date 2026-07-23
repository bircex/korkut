using Korkut.Application.Common;
using Korkut.Domain.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Common;

/// <summary>
/// The EF base every aggregate repository derives from, and the only place outside its
/// subclasses that touches a <see cref="KorkutDbContext"/>. <see cref="Scoped"/> pins the
/// office on every query, so tenancy holds even before Postgres RLS is consulted.
/// </summary>
public abstract class Repository<TEntity>(KorkutDbContext db) : IRepository<TEntity>
    where TEntity : Entity, ITenantOwned, IAggregateRoot
{
    protected KorkutDbContext Db => db;

    protected DbSet<TEntity> Set => db.Set<TEntity>();

    /// <summary>Every read starts here: the aggregate, pinned to one office.</summary>
    protected IQueryable<TEntity> Scoped(Guid officeId) => Set.Where(e => e.OfficeId == officeId);

    public virtual Task<TEntity?> FindAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);

    public virtual Task<TEntity?> FindForUpdateAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => Scoped(officeId).SingleOrDefaultAsync(e => e.Id == id, ct);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        Set.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public virtual async Task<bool> RemoveAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await Scoped(officeId).Where(e => e.Id == id).ExecuteDeleteAsync(ct) > 0;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
