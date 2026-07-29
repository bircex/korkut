using Korkut.Hearings.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Hearings.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IHearingRepository"/> over the ambient DbContext.</summary>
internal sealed class HearingRepository(DbContext db) : Repository<Hearing>(db), IHearingRepository
{
    public async Task<IReadOnlyList<Hearing>> ListForMatterAsync(
        Guid officeId, Guid matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(h => h.MatterId == matterId).OrderBy(h => h.ScheduledAt).ToListAsync(ct);
}
