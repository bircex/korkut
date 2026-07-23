using Korkut.Application.Common;
using Korkut.Domain.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Common;

/// <summary>
/// EF base for soft-deletable aggregates: <see cref="Scoped"/> additionally filters out
/// retired rows, so no caller can accidentally read a deleted record.
/// </summary>
public abstract class SoftDeleteRepository<TEntity>(KorkutDbContext db)
    : Repository<TEntity>(db), ISoftDeleteRepository<TEntity>
    where TEntity : Entity, ITenantOwned, IAggregateRoot, ISoftDeletable
{
    protected new IQueryable<TEntity> Scoped(Guid officeId) =>
        base.Scoped(officeId).Where(e => e.DeletedAt == null);

    public override Task<TEntity?> FindAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);

    public override async Task<IReadOnlyList<TEntity>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().ToListAsync(ct);

    public override Task<TEntity?> FindForUpdateAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => Scoped(officeId).SingleOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> SoftDeleteAsync(Guid officeId, Guid id, DateTimeOffset deletedAt, CancellationToken ct = default)
        => SoftDeleteCoreAsync(officeId, id, deletedAt, ct);

    private async Task<bool> SoftDeleteCoreAsync(Guid officeId, Guid id, DateTimeOffset deletedAt, CancellationToken ct)
        => await Scoped(officeId).Where(e => e.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DeletedAt, deletedAt), ct) > 0;
}
