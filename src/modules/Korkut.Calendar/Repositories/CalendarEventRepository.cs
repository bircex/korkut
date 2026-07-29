using Korkut.Calendar.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Calendar.Repositories;

/// <summary>EF/Postgres adapter for <see cref="ICalendarEventRepository"/> over the ambient DbContext.</summary>
internal sealed class CalendarEventRepository(DbContext db)
    : Repository<CalendarEvent>(db), ICalendarEventRepository
{
    public async Task<IReadOnlyList<CalendarEvent>> ListAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(e => (start == null || e.StartAt >= start)
                && (end == null || e.StartAt <= end)
                && (category == null || e.Category == category)
                && (matterId == null || e.MatterId == matterId))
            .OrderBy(e => e.StartAt)
            .ToListAsync(ct);
}
