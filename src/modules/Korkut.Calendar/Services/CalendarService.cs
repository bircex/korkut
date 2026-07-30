using Korkut.Calendar.Repositories;
using Korkut.Calendar.Entities;
using Korkut.Contract;
using Korkut.Contract.Calendar;
using Korkut.Contract.Clients;
using Korkut.Contract.Deadlines;
using Korkut.Contract.Hearings;
using Korkut.Contract.Matters;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Calendar.Services;

/// <summary>
/// Calendar use-cases over the event aggregate. The unified view (events ∪ deadlines ∪ hearings,
/// enriched with matter/client/assignee names) is COMPOSED here by calling each owner's Contract
/// lookup — the calendar never joins another module's table. Names are resolved in one batch per
/// kind to keep the fan-out flat. Every call RETURNS a <see cref="Result"/>.
/// </summary>
internal sealed class CalendarService(
    ICalendarEventRepository events,
    IDeadlineLookup deadlines,
    IHearingLookup hearings,
    IMatterLookup matters,
    IClientLookup clients,
    IUserLookup users,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ICalendarService
{
    /// <summary>The agenda look-ahead window, in days, clamped to a sane range.</summary>
    private const int DefaultAgendaDays = 14;

    private static readonly string[] EventCategories = ["appointment", "reminder", "hearing", "deadline"];

    private static DateTimeOffset AtMidnightUtc(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public Task<Result<IReadOnlyList<CalendarEntryResponse>>> ListEntriesAsync(
        DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<CalendarEntryResponse>>.Ok(await BuildEntriesAsync(office, start, end, category, matterId, ct)), ct);

    public Task<Result<CalendarEntryResponse>> CreateAsync(CreateCalendarEventRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var calendarEvent = new CalendarEvent(
                office, request.Category!, request.Title!.Trim(), request.StartAt, request.EndAt, request.AllDay,
                request.Type?.Trim(), request.MatterId, request.ClientId, request.AssigneeId,
                request.Location?.Trim(), request.Notes?.Trim(), request.Rrule?.Trim(), request.RemindBeforeMin,
                clock.UtcNow);
            await events.AddAsync(calendarEvent, ct);

            return Result<CalendarEntryResponse>.Ok(await EnrichEventAsync(office, calendarEvent, ct));
        }, ct);

    public Task<Result<CalendarEntryResponse>> UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var calendarEvent = await events.FindForUpdateAsync(office, id, ct);
            if (calendarEvent is null)
            {
                return Result<CalendarEntryResponse>.NotFound("Takvim kaydı bulunamadı.");
            }

            calendarEvent.Update(
                request.Title?.Trim(), request.Type?.Trim(), request.StartAt, request.EndAt, request.AllDay,
                request.MatterId, request.ClientId, request.AssigneeId, request.Location?.Trim(), request.Status,
                request.Result?.Trim(), request.Notes?.Trim(), request.Rrule?.Trim(), request.RemindBeforeMin);
            await events.SaveChangesAsync(ct);
            return Result<CalendarEntryResponse>.Ok(await EnrichEventAsync(office, calendarEvent, ct));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await events.RemoveAsync(office, id, ct)
                ? Result.Ok()
                : Result.NotFound("Takvim kaydı bulunamadı."), ct);

    public Task<Result<AgendaResponse>> AgendaAsync(int days, CancellationToken ct = default)
    {
        var window = Math.Clamp(days <= 0 ? DefaultAgendaDays : days, 1, 365);
        return InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var until = now.AddDays(window);

            var hearingRows = await hearings.ListInRangeAsync(office, now, until, null, ct);
            // Include today's / just-overdue open deadlines (from a day before now through the window).
            var deadlineRows = (await deadlines.ListInRangeAsync(office, now.AddDays(-1), until, null, ct))
                .Where(d => d.CompletedAt == null).ToList();

            var titles = await matters.TitlesAsync(office, Ids(hearingRows.Select(h => (Guid?)h.MatterId)), ct);

            var items = hearingRows
                .Select(h => new AgendaItemResponse(
                    "hearing", h.ScheduledAt, h.Id, h.MatterId, titles.GetValueOrDefault(h.MatterId, string.Empty), "scheduled",
                    h.Type, TimeOnly.FromDateTime(h.ScheduledAt.DateTime), h.Location))
                .Concat(deadlineRows.Select(d => new AgendaItemResponse(
                    "deadline", AtMidnightUtc(d.DueDate), d.Id, d.MatterId, d.Title, "open")))
                .OrderBy(r => r.Date)
                .ToList();

            return Result<AgendaResponse>.Ok(new AgendaResponse(items, window));
        }, ct);
    }

    /// <summary>Compose the unified entry list by fetching each kind from its owner and enriching the names in one pass.</summary>
    private async Task<IReadOnlyList<CalendarEntryResponse>> BuildEntriesAsync(
        Guid office, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct)
    {
        IReadOnlyList<CalendarEvent> eventRows = category is null || EventCategories.Contains(category)
            ? await events.ListAsync(office, start, end, category, matterId, ct)
            : [];
        IReadOnlyList<DeadlineResponse> deadlineRows = category is null or "deadline"
            ? await deadlines.ListInRangeAsync(office, start, end, matterId, ct)
            : [];
        IReadOnlyList<HearingCalendarRow> hearingRows = category is null or "hearing"
            ? await hearings.ListInRangeAsync(office, start, end, matterId, ct)
            : [];

        var (titles, names, emails) = await ResolveNamesAsync(office,
            eventRows.Select(e => e.MatterId).Concat(deadlineRows.Select(d => d.MatterId)).Concat(hearingRows.Select(h => (Guid?)h.MatterId)),
            eventRows.Select(e => e.ClientId),
            eventRows.Select(e => e.AssigneeId), ct);

        var rows = new List<CalendarEntryResponse>();
        rows.AddRange(eventRows.Select(e => ToResponse(e, titles, names, emails)));
        rows.AddRange(deadlineRows.Select(d => new CalendarEntryResponse(
            d.Id, "deadline", d.Type, d.Title, AtMidnightUtc(d.DueDate), null, true,
            d.MatterId, Name(titles, d.MatterId), null, null, null, null,
            null, d.CompletedAt == null ? "scheduled" : "done", null, null, null, null, "pending", false)));
        rows.AddRange(hearingRows.Select(h => new CalendarEntryResponse(
            h.Id, "hearing", h.Type, titles.GetValueOrDefault(h.MatterId, string.Empty), h.ScheduledAt, null, false,
            h.MatterId, titles.GetValueOrDefault(h.MatterId, string.Empty), null, null, null, null,
            h.Location, "scheduled", null, null, null, null, "pending", false)));

        return rows.OrderBy(r => r.StartAt).ToList();
    }

    private async Task<CalendarEntryResponse> EnrichEventAsync(Guid office, CalendarEvent e, CancellationToken ct)
    {
        var (titles, names, emails) = await ResolveNamesAsync(office, [e.MatterId], [e.ClientId], [e.AssigneeId], ct);
        return ToResponse(e, titles, names, emails);
    }

    private async Task<(IReadOnlyDictionary<Guid, string> Titles, IReadOnlyDictionary<Guid, string> Names, IReadOnlyDictionary<Guid, string> Emails)>
        ResolveNamesAsync(Guid office, IEnumerable<Guid?> matterIds, IEnumerable<Guid?> clientIds, IEnumerable<Guid?> assigneeIds, CancellationToken ct)
    {
        var titles = await matters.TitlesAsync(office, Ids(matterIds), ct);
        var names = await clients.NamesAsync(office, Ids(clientIds), ct);
        var emails = await users.EmailsAsync(office, Ids(assigneeIds), ct);
        return (titles, names, emails);
    }

    private static CalendarEntryResponse ToResponse(
        CalendarEvent e, IReadOnlyDictionary<Guid, string> titles, IReadOnlyDictionary<Guid, string> names, IReadOnlyDictionary<Guid, string> emails)
        => new(
            e.Id, e.Category, e.Type, e.Title, e.StartAt, e.EndAt, e.AllDay,
            e.MatterId, Name(titles, e.MatterId), e.ClientId, Name(names, e.ClientId),
            e.AssigneeId, Name(emails, e.AssigneeId), e.Location, e.Status,
            e.Result, e.Notes, e.Rrule, e.RemindBeforeMin, e.ReminderStatus, true);

    private static string? Name(IReadOnlyDictionary<Guid, string> map, Guid? id) =>
        id is { } key && map.TryGetValue(key, out var name) ? name : null;

    private static List<Guid> Ids(IEnumerable<Guid?> ids) =>
        ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
