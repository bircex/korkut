using Korkut.Calendar.Entities;
using Korkut.Contract;
using Korkut.Contract.Calendar;
using Korkut.Core;

namespace Korkut.Calendar;

/// <summary>
/// Calendar use-cases over the event aggregate and the cross-module read query. Create and patch
/// re-read through the unified view, so the response carries the matter/client/assignee names.
/// Every call RETURNS a <see cref="Result"/>.
/// </summary>
internal sealed class CalendarService(
    ICalendarEventRepository events, ICalendarQuery query,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ICalendarService
{
    /// <summary>The agenda look-ahead window, in days, clamped to a sane range.</summary>
    private const int DefaultAgendaDays = 14;

    public Task<Result<IReadOnlyList<CalendarEntryResponse>>> ListEntriesAsync(
        DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<CalendarEntryResponse>>.Ok(await query.ListEntriesAsync(office, start, end, category, matterId, ct)), ct);

    public Task<Result<CalendarEntryResponse>> CreateAsync(CreateCalendarEventRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var calendarEvent = new CalendarEvent(
                office, request.Category!, request.Title!.Trim(), request.StartAt, request.EndAt, request.AllDay,
                request.Type?.Trim(), request.MatterId, request.ClientId, request.AssigneeId,
                request.Location?.Trim(), request.Notes?.Trim(), request.Rrule?.Trim(), request.RemindBeforeMin,
                clock.UtcNow);
            await events.AddAsync(calendarEvent, ct);

            // Re-read through the unified view so the response carries the display names.
            return Result<CalendarEntryResponse>.Ok((await query.GetEditableEntryAsync(office, calendarEvent.Id, ct))!);
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
            return Result<CalendarEntryResponse>.Ok((await query.GetEditableEntryAsync(office, id, ct))!);
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
            var items = await query.AgendaAsync(office, now, now.AddDays(window), ct);
            return Result<AgendaResponse>.Ok(new AgendaResponse(items, window));
        }, ct);
    }

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
