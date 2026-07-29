using Korkut.Calendar.Entities;
using Korkut.Core;

namespace Korkut.Calendar.Repositories;

/// <summary>Persistence port for the editable calendar-event aggregate.</summary>
internal interface ICalendarEventRepository : IRepository<CalendarEvent>
{
    /// <summary>The office's events, optionally filtered by [start, end] on the start time, category, and matter.</summary>
    Task<IReadOnlyList<CalendarEvent>> ListAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default);
}
