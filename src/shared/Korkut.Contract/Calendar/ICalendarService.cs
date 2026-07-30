namespace Korkut.Contract.Calendar;

/// <summary>
/// Calendar read use-cases (cross-module contract). The calendar owns no table of its own: the
/// unified view is COMPOSED from the office's agenda records (görev/hatırlatma/randevu, owned by
/// Tasks) plus hearings and deadlines surfaced read-only from their domains. Editable records are
/// created and patched through the Tasks API. Calls RETURN a <see cref="Result"/>.
/// </summary>
public interface ICalendarService
{
    Task<Result<IReadOnlyList<CalendarEntryResponse>>> ListEntriesAsync(
        DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default);

    /// <summary>Upcoming hearings + open deadlines within the look-ahead window.</summary>
    Task<Result<AgendaResponse>> AgendaAsync(int days, CancellationToken ct = default);
}
