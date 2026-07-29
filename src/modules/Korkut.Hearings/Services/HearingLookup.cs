using Korkut.Hearings.Repositories;
using Korkut.Hearings.Entities;
using Korkut.Contract.Hearings;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Hearings.Services;

/// <summary>
/// Implements the cross-module <see cref="IHearingLookup"/> over the hearing repository / the
/// module's own table, reading in the caller's already-open tenant scope. Lets other modules read
/// hearings without the Hearing type.
/// </summary>
internal sealed class HearingLookup(IHearingRepository repository, DbContext db) : IHearingLookup
{
    public async Task<IReadOnlyList<HearingResponse>> ListForMatterAsync(
        Guid officeId, Guid matterId, CancellationToken ct = default)
        => (await repository.ListForMatterAsync(officeId, matterId, ct)).Select(HearingMapping.ToResponse).ToList();

    public async Task<IReadOnlyList<HearingCalendarRow>> ListInRangeAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, Guid? matterId, CancellationToken ct = default)
        => await db.Set<Hearing>().AsNoTracking()
            .Where(h => h.OfficeId == officeId
                && (matterId == null || h.MatterId == matterId)
                && (start == null || h.ScheduledAt >= start)
                && (end == null || h.ScheduledAt <= end))
            .OrderBy(h => h.ScheduledAt)
            .Select(h => new HearingCalendarRow(h.Id, h.MatterId, h.ScheduledAt))
            .ToListAsync(ct);

    public async Task<int> CountInRangeAsync(Guid officeId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
        => await db.Set<Hearing>().AsNoTracking()
            .CountAsync(h => h.OfficeId == officeId && h.ScheduledAt >= start && h.ScheduledAt <= end, ct);
}
