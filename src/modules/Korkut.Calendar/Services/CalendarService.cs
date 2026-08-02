using Korkut.Contract;
using Korkut.Contract.Calendar;
using Korkut.Contract.Clients;
using Korkut.Contract.Deadlines;
using Korkut.Contract.Hearings;
using Korkut.Contract.Matters;
using Korkut.Contract.Organization;
using Korkut.Contract.Tasks;
using Korkut.Core;

namespace Korkut.Calendar.Services;

/// <summary>
/// Calendar read use-cases. The calendar owns no table: the unified view (agenda records ∪ deadlines
/// ∪ hearings, enriched with matter/client/assignee names) is COMPOSED here by calling each owner's
/// Contract lookup — never by joining another module's table. Editable records (görev/hatırlatma/
/// randevu) live in Tasks; deadlines and hearings surface read-only from their domains. Names are
/// resolved in one batch per kind to keep the fan-out flat. Every call RETURNS a <see cref="Result"/>.
/// </summary>
internal sealed class CalendarService(
    ITaskLookup tasks,
    IDeadlineLookup deadlines,
    IHearingLookup hearings,
    IMatterLookup matters,
    IClientLookup clients,
    IUserLookup users,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ICalendarService
{
    /// <summary>The agenda look-ahead window, in days, clamped to a sane range.</summary>
    private const int DefaultAgendaDays = 14;

    /// <summary>The editable agenda categories owned by Tasks.</summary>
    private static readonly string[] TaskCategories = ["gorev", "hatirlatma", "randevu"];

    private static DateTimeOffset AtMidnightUtc(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public Task<Result<IReadOnlyList<CalendarEntryResponse>>> ListEntriesAsync(
        DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<CalendarEntryResponse>>.Ok(await BuildEntriesAsync(office, start, end, category, matterId, ct)), ct);

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
        IReadOnlyList<TaskCalendarRow> taskRows = category is null || TaskCategories.Contains(category)
            ? await tasks.ListInRangeAsync(office, start, end, matterId, ct)
            : [];
        if (category is not null && TaskCategories.Contains(category))
        {
            taskRows = taskRows.Where(t => t.Category == category).ToList();
        }

        IReadOnlyList<DeadlineResponse> deadlineRows = category is null or "deadline"
            ? await deadlines.ListInRangeAsync(office, start, end, matterId, ct)
            : [];
        IReadOnlyList<HearingCalendarRow> hearingRows = category is null or "hearing"
            ? await hearings.ListInRangeAsync(office, start, end, matterId, ct)
            : [];

        var (titles, names, emails) = await ResolveNamesAsync(office,
            taskRows.Select(t => t.MatterId).Concat(deadlineRows.Select(d => d.MatterId)).Concat(hearingRows.Select(h => (Guid?)h.MatterId)),
            taskRows.Select(t => t.ClientId),
            taskRows.Select(t => t.AssigneeId), ct);

        var rows = new List<CalendarEntryResponse>();
        rows.AddRange(taskRows.Select(t => new CalendarEntryResponse(
            t.Id, t.Category, t.Type, t.Title, t.StartAt, t.EndAt, t.AllDay,
            t.MatterId, Name(titles, t.MatterId), t.ClientId, Name(names, t.ClientId),
            t.AssigneeId, Name(emails, t.AssigneeId), t.Location, t.Status,
            null, t.Description, t.Rrule, t.RemindBeforeMin, t.ReminderStatus, true)));
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

    private async Task<(IReadOnlyDictionary<Guid, string> Titles, IReadOnlyDictionary<Guid, string> Names, IReadOnlyDictionary<Guid, string> Emails)>
        ResolveNamesAsync(Guid office, IEnumerable<Guid?> matterIds, IEnumerable<Guid?> clientIds, IEnumerable<Guid?> assigneeIds, CancellationToken ct)
    {
        var titles = await matters.TitlesAsync(office, Ids(matterIds), ct);
        var names = await clients.NamesAsync(office, Ids(clientIds), ct);
        var emails = await users.DisplayNamesAsync(office, Ids(assigneeIds), ct);
        return (titles, names, emails);
    }

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
