using Korkut.Application.Offices;
using Korkut.Domain.Offices;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Offices;

/// <summary>
/// EF/Postgres adapter for <see cref="IOfficeRepository"/>. No office scoping here by
/// design: this IS the tenant table, so it cannot derive from the tenant-scoped EF base.
/// </summary>
public sealed class OfficeRepository(KorkutDbContext db) : IOfficeRepository
{
    public Task<Office?> FindAsync(Guid officeId, CancellationToken ct = default)
        => db.Offices.AsNoTracking().SingleOrDefaultAsync(o => o.Id == officeId, ct);

    public Task<Office?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default)
        => db.Offices.SingleOrDefaultAsync(o => o.Id == officeId, ct);

    public async Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct = default)
        => await db.Offices.AsNoTracking().OrderBy(o => o.Name).ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
