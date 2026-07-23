using Korkut.Application.Calendar.Responses;

namespace Korkut.Application.Calendar;

/// <summary>
/// The calendar READ side. It spans three aggregates (calendar events, hearings, deadlines)
/// and enriches rows with matter/client/assignee display names, so it is a query port in its
/// own right rather than a repository for any one aggregate.
/// </summary>
public interface ICalendarQuery
{
    /// <summary>The unified entry list (events ∪ deadlines ∪ hearings), earliest first.</summary>
    Task<IReadOnlyList<CalendarEntryResponse>> ListEntriesAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default);

    /// <summary>A single editable entry (enriched), or null if the id is not an event row.</summary>
    Task<CalendarEntryResponse?> GetEditableEntryAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Hearings in [now, until] and open deadlines due from a day before now through until.</summary>
    Task<IReadOnlyList<AgendaItemResponse>> AgendaAsync(
        Guid officeId, DateTimeOffset now, DateTimeOffset until, CancellationToken ct = default);
}
