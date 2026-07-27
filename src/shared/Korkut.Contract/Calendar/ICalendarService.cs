namespace Korkut.Contract.Calendar;

/// <summary>
/// Calendar use-cases (cross-module contract). Writes apply to editable calendar events only; a
/// hearing or deadline id is NotFound here, since those are edited through their own domains.
/// Calls RETURN a <see cref="Result"/>.
/// </summary>
public interface ICalendarService
{
    Task<Result<IReadOnlyList<CalendarEntryResponse>>> ListEntriesAsync(
        DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default);

    Task<Result<CalendarEntryResponse>> CreateAsync(CreateCalendarEventRequest request, CancellationToken ct = default);

    Task<Result<CalendarEntryResponse>> UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Upcoming hearings + open deadlines within the look-ahead window.</summary>
    Task<Result<AgendaResponse>> AgendaAsync(int days, CancellationToken ct = default);
}
