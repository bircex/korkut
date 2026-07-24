using Korkut.Application.Correspondence;
using Korkut.Domain.Correspondence;
using Korkut.Persistence;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Correspondence;

/// <summary>EF/Postgres adapter for <see cref="ICorrespondenceRepository"/>.</summary>
public sealed class CorrespondenceRepository(KorkutDbContext db)
    : Repository<CorrespondenceItem>(db), ICorrespondenceRepository
{
    public async Task<IReadOnlyList<CorrespondenceItem>> ListAsync(
        Guid officeId, string? direction, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(c => direction == null || c.Direction == direction)
            .OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
}
