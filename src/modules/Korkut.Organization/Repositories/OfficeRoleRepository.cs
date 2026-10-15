using Korkut.Organization.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IOfficeRoleRepository"/> over the ambient DbContext.</summary>
internal sealed class OfficeRoleRepository(DbContext db) : IOfficeRoleRepository
{
    public async Task<IReadOnlyList<OfficeRole>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await db.Set<OfficeRole>().AsNoTracking().Where(r => r.OfficeId == officeId).OrderBy(r => r.CreatedAt).ToListAsync(ct);

    public Task<OfficeRole?> FindAsync(Guid officeId, string key, CancellationToken ct = default)
        => db.Set<OfficeRole>().AsNoTracking().SingleOrDefaultAsync(r => r.OfficeId == officeId && r.Key == key, ct);

    public Task<OfficeRole?> FindForUpdateAsync(Guid officeId, string key, CancellationToken ct = default)
        => db.Set<OfficeRole>().SingleOrDefaultAsync(r => r.OfficeId == officeId && r.Key == key, ct);

    public void Add(OfficeRole role) => db.Set<OfficeRole>().Add(role);

    public void Remove(OfficeRole role) => db.Set<OfficeRole>().Remove(role);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
