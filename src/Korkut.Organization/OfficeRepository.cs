using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization;

/// <summary>
/// EF/Postgres adapter for <see cref="IOfficeRepository"/> over the ambient DbContext. No office
/// scoping here by design: this IS the tenant table, so it cannot derive from the tenant-scoped base.
/// </summary>
public sealed class OfficeRepository(DbContext db) : IOfficeRepository
{
    public Task<Office?> FindAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<Office>().AsNoTracking().SingleOrDefaultAsync(o => o.Id == officeId, ct);

    public Task<Office?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<Office>().SingleOrDefaultAsync(o => o.Id == officeId, ct);

    public async Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct = default)
        => await db.Set<Office>().AsNoTracking().OrderBy(o => o.Name).ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
