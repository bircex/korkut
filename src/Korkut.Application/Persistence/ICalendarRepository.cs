using Korkut.Domain.Calendar;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for the calendar. Writes touch only <c>calendar_events</c>; reads
/// project a UNIFIED view that also merges the office's read-only hearings and deadlines.
/// </summary>
public interface ICalendarRepository
{
    Task<Guid> AddEventAsync(Guid officeId, CalendarEventDraft draft, DateTimeOffset createdAt, CancellationToken ct = default);

    /// <summary>Patch an editable event; false if it doesn't exist.</summary>
    Task<bool> UpdateEventAsync(Guid officeId, Guid id, CalendarEventPatch patch, CancellationToken ct = default);

    /// <summary>Delete an editable event; false if it doesn't exist.</summary>
    Task<bool> DeleteEventAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>The unified entry list (calendar_events ∪ deadlines ∪ hearings), earliest first.</summary>
    Task<IReadOnlyList<CalendarEntry>> ListEntriesAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default);

    /// <summary>A single editable entry (enriched), or null if it isn't a calendar_events row.</summary>
    Task<CalendarEntry?> GetEditableEntryAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Hearings in [<paramref name="now"/>, <paramref name="until"/>] and open deadlines
    /// due from a day before <paramref name="now"/> through <paramref name="until"/>.</summary>
    Task<IReadOnlyList<AgendaItem>> AgendaAsync(Guid officeId, DateTimeOffset now, DateTimeOffset until, CancellationToken ct = default);
}
