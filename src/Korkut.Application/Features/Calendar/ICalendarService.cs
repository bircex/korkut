using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Calendar;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Calendar;

/// <summary>
/// Calendar use-cases. CRUD applies to editable <c>calendar_events</c> only; the list and
/// agenda reads merge the office's real hearings and deadlines (owned by their own domains).
/// Create/patch re-read through the unified view so the result carries display enrichment.
/// </summary>
public interface ICalendarService
{
    Task<IReadOnlyList<CalendarEntry>> ListEntriesAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default);

    Task<CalendarEntry> CreateEventAsync(Guid officeId, CalendarEventDraft draft, CancellationToken ct = default);

    /// <summary>Patch an editable event and return the enriched entry, or null if missing.</summary>
    Task<CalendarEntry?> UpdateEventAsync(Guid officeId, Guid id, CalendarEventPatch patch, CancellationToken ct = default);

    Task<bool> DeleteEventAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Upcoming hearings + open deadlines within <paramref name="days"/> days.</summary>
    Task<IReadOnlyList<AgendaItem>> AgendaAsync(Guid officeId, int days, CancellationToken ct = default);
}

/// <summary>Calendar orchestration over the repository port.</summary>
public sealed class CalendarService(ICalendarRepository repo, IClock clock) : ICalendarService
{
    public Task<IReadOnlyList<CalendarEntry>> ListEntriesAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default)
        => repo.ListEntriesAsync(officeId, start, end, category, matterId, ct);

    public async Task<CalendarEntry> CreateEventAsync(Guid officeId, CalendarEventDraft draft, CancellationToken ct = default)
    {
        var id = await repo.AddEventAsync(officeId, draft, clock.UtcNow, ct);
        // Re-read through the unified view so the response carries matter/client/assignee names.
        return (await repo.GetEditableEntryAsync(officeId, id, ct))!;
    }

    public async Task<CalendarEntry?> UpdateEventAsync(Guid officeId, Guid id, CalendarEventPatch patch, CancellationToken ct = default)
        => await repo.UpdateEventAsync(officeId, id, patch, ct) ? await repo.GetEditableEntryAsync(officeId, id, ct) : null;

    public Task<bool> DeleteEventAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.DeleteEventAsync(officeId, id, ct);

    public Task<IReadOnlyList<AgendaItem>> AgendaAsync(Guid officeId, int days, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return repo.AgendaAsync(officeId, now, now.AddDays(days), ct);
    }
}
