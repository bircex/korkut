using Korkut.Application.Hearings;
using Korkut.Domain.Hearings;
using Korkut.Persistence;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Hearings;

/// <summary>EF/Postgres adapter for <see cref="IHearingRepository"/>.</summary>
public sealed class HearingRepository(KorkutDbContext db) : Repository<Hearing>(db), IHearingRepository
{
    public async Task<IReadOnlyList<Hearing>> ListForMatterAsync(
        Guid officeId, Guid matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(h => h.MatterId == matterId).OrderBy(h => h.ScheduledAt).ToListAsync(ct);
}
