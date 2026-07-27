using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Correspondence;

/// <summary>EF/Postgres adapter for <see cref="ICorrespondenceRepository"/> over the ambient DbContext.</summary>
public sealed class CorrespondenceRepository(DbContext db)
    : Repository<CorrespondenceItem>(db), ICorrespondenceRepository
{
    public async Task<IReadOnlyList<CorrespondenceItem>> ListAsync(
        Guid officeId, string? direction, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(c => direction == null || c.Direction == direction)
            .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
}
