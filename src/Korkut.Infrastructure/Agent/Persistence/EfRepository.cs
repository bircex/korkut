using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
using Korkut.Domain.Common;

namespace Korkut.Infrastructure.Agent.Persistence;

/// <summary>
/// EF Core base for the Agent DB repositories — the ONLY place a DbContext lives. Bakes in the
/// mandatory tenancy: <see cref="Scoped"/> is the office-filtered starting point for every read
/// (belt-and-suspenders atop RLS), and the generic CRUD always takes the office. Concrete repos
/// inherit the context access + <see cref="Scoped"/> and add their domain-returning queries.
/// </summary>
public abstract class EfRepository<TEntity>(AgentDbContext db) : IRepository<TEntity>
    where TEntity : class, ITenantOwned, IEntity
{
    protected AgentDbContext Db => db;

    protected DbSet<TEntity> Set => db.Set<TEntity>();

    /// <summary>Office-scoped, no-tracking query: the mandatory starting point for every read.</summary>
    protected IQueryable<TEntity> Scoped(Guid officeId) => Set.AsNoTracking().Where(e => e.OfficeId == officeId);

    public Task<TEntity?> FindAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => Scoped(officeId).SingleOrDefaultAsync(e => e.Id == id, ct);

    public async Task InsertAsync(TEntity entity, CancellationToken ct = default)
    {
        Set.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await Set.Where(e => e.OfficeId == officeId && e.Id == id).ExecuteDeleteAsync(ct) > 0;

    public Task<int> SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
