using Korkut.Application.Calendar.Requests;
using Korkut.Application.Calendar.Responses;

namespace Korkut.Application.Calendar;

/// <summary>
/// Calendar use-cases. Writes apply to editable calendar events only; a hearing or deadline
/// id raises <c>NotFoundException</c> here, since those are edited through their own domains.
/// </summary>
public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEntryResponse>> ListEntriesAsync(
        DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default);

    Task<CalendarEntryResponse> CreateAsync(CreateCalendarEventRequest request, CancellationToken ct = default);

    Task<CalendarEntryResponse> UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Upcoming hearings + open deadlines within the look-ahead window.</summary>
    Task<AgendaResponse> AgendaAsync(int days, CancellationToken ct = default);
}
