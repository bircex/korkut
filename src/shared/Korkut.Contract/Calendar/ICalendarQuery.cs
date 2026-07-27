namespace Korkut.Contract.Calendar;

/// <summary>
/// The calendar READ side — a cross-module read-model. It spans three aggregates (calendar events,
/// hearings, deadlines) and enriches rows with matter/client/assignee display names, so its impl
/// lives at the host/infrastructure level (which can see every table), not inside any one module.
/// The port is in Contract so a module service can depend on it.
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
