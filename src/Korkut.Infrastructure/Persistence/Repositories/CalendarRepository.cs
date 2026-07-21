using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Calendar;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="ICalendarRepository"/>. Writes touch only
/// <c>calendar_events</c>; the list/agenda reads project a unified view that merges the
/// office's read-only hearings and deadlines (owned by their own domains). Tenant context
/// is set by the caller (RLS).
/// </summary>
public sealed class CalendarRepository(KorkutDbContext db) : ICalendarRepository
{
    private static DateTimeOffset AtMidnightUtc(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public async Task<Guid> AddEventAsync(Guid officeId, CalendarEventDraft draft, DateTimeOffset createdAt, CancellationToken ct = default)
    {
        var e = new CalendarEventEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Category = draft.Category,
            Type = draft.Type,
            Title = draft.Title,
            StartAt = draft.StartAt,
            EndAt = draft.EndAt,
            AllDay = draft.AllDay,
            MatterId = draft.MatterId,
            ClientId = draft.ClientId,
            AssigneeId = draft.AssigneeId,
            Location = draft.Location,
            Notes = draft.Notes,
            Rrule = draft.Rrule,
            RemindBeforeMin = draft.RemindBeforeMin,
            CreatedAt = createdAt,
        };
        db.CalendarEvents.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task<bool> UpdateEventAsync(Guid officeId, Guid id, CalendarEventPatch patch, CancellationToken ct = default)
        => await db.CalendarEvents
            .Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Title, x => patch.Title ?? x.Title)
                .SetProperty(x => x.Type, x => patch.Type ?? x.Type)
                .SetProperty(x => x.StartAt, x => patch.StartAt ?? x.StartAt)
                .SetProperty(x => x.EndAt, x => patch.EndAt ?? x.EndAt)
                .SetProperty(x => x.AllDay, x => patch.AllDay ?? x.AllDay)
                .SetProperty(x => x.MatterId, x => patch.MatterId ?? x.MatterId)
                .SetProperty(x => x.ClientId, x => patch.ClientId ?? x.ClientId)
                .SetProperty(x => x.AssigneeId, x => patch.AssigneeId ?? x.AssigneeId)
                .SetProperty(x => x.Location, x => patch.Location ?? x.Location)
                .SetProperty(x => x.Status, x => patch.Status ?? x.Status)
                .SetProperty(x => x.Result, x => patch.Result ?? x.Result)
                .SetProperty(x => x.Notes, x => patch.Notes ?? x.Notes)
                .SetProperty(x => x.Rrule, x => patch.Rrule ?? x.Rrule)
                .SetProperty(x => x.RemindBeforeMin, x => patch.RemindBeforeMin ?? x.RemindBeforeMin), ct) > 0;

    public async Task<bool> DeleteEventAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.CalendarEvents.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<IReadOnlyList<CalendarEntry>> ListEntriesAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default)
    {
        var rows = new List<CalendarEntry>();

        // Editable calendar_events, enriched with matter/client/assignee display fields.
        if (category is null or "appointment" or "reminder" or "hearing" or "deadline")
        {
            var events = await EditableProjection(
                db.CalendarEvents.AsNoTracking().Where(e => e.OfficeId == officeId
                    && (start == null || e.StartAt >= start)
                    && (end == null || e.StartAt <= end)
                    && (category == null || e.Category == category)
                    && (matterId == null || e.MatterId == matterId)))
                .ToListAsync(ct);
            rows.AddRange(events);
        }

        // Read-only deadlines as category="deadline".
        if (category is null or "deadline")
        {
            var deadlines = await (
                from d in db.Deadlines.AsNoTracking()
                where d.OfficeId == officeId && (matterId == null || d.MatterId == matterId)
                join m in db.Matters.AsNoTracking() on new { d.OfficeId, Id = d.MatterId } equals new { m.OfficeId, Id = (Guid?)m.Id } into mj
                from m in mj.DefaultIfEmpty()
                select new { d.Id, d.Title, d.DueDate, d.Kind, d.MatterId, MatterTitle = m != null ? m.Title : null, d.CompletedAt })
                .ToListAsync(ct);

            rows.AddRange(deadlines
                .Select(d => new CalendarEntry(
                    d.Id, "deadline", d.Kind, d.Title, AtMidnightUtc(d.DueDate), null, true,
                    d.MatterId, d.MatterTitle, null, null, null, null, null,
                    d.CompletedAt == null ? "scheduled" : "done", null, null, null, null, "pending", false))
                .Where(r => (start == null || r.StartAt >= start) && (end == null || r.StartAt <= end)));
        }

        // Read-only hearings as category="hearing".
        if (category is null or "hearing")
        {
            var hearings = await (
                from h in db.MatterHearings.AsNoTracking()
                where h.OfficeId == officeId && (matterId == null || h.MatterId == matterId)
                    && (start == null || h.ScheduledAt >= start) && (end == null || h.ScheduledAt <= end)
                join m in db.Matters.AsNoTracking() on new { h.OfficeId, h.MatterId } equals new { m.OfficeId, MatterId = m.Id }
                select new CalendarEntry(
                    h.Id, "hearing", null, m.Title, h.ScheduledAt, null, false,
                    h.MatterId, m.Title, null, null, null, null, null, "scheduled", null, null, null, null, "pending", false))
                .ToListAsync(ct);
            rows.AddRange(hearings);
        }

        return rows.OrderBy(r => r.StartAt).ToList();
    }

    public async Task<CalendarEntry?> GetEditableEntryAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await EditableProjection(
                db.CalendarEvents.AsNoTracking().Where(e => e.OfficeId == officeId && e.Id == id))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AgendaItem>> AgendaAsync(Guid officeId, DateTimeOffset now, DateTimeOffset until, CancellationToken ct = default)
    {
        var hearings = await (
            from h in db.MatterHearings.AsNoTracking()
            where h.OfficeId == officeId && h.ScheduledAt >= now && h.ScheduledAt <= until
            join m in db.Matters.AsNoTracking() on new { h.OfficeId, h.MatterId } equals new { m.OfficeId, MatterId = m.Id }
            select new AgendaItem("hearing", h.ScheduledAt, h.Id, h.MatterId, m.Title, "scheduled"))
            .ToListAsync(ct);

        var deadlines = await db.Deadlines.AsNoTracking()
            .Where(d => d.OfficeId == officeId && d.CompletedAt == null)
            .Select(d => new { d.Id, d.Title, d.DueDate, d.MatterId })
            .ToListAsync(ct);

        var deadlineRows = deadlines
            .Select(d => new AgendaItem("deadline", AtMidnightUtc(d.DueDate), d.Id, d.MatterId, d.Title, "open"))
            .Where(r => r.Date >= now.AddDays(-1) && r.Date <= until); // include today's/just-overdue deadlines

        return hearings.Concat(deadlineRows).OrderBy(r => r.Date).ToList();
    }

    /// <summary>Enrich a (pre-filtered) calendar_events query with matter/client/assignee names.</summary>
    private IQueryable<CalendarEntry> EditableProjection(IQueryable<CalendarEventEntity> events) =>
        from e in events
        join m in db.Matters.AsNoTracking() on new { e.OfficeId, Id = e.MatterId } equals new { m.OfficeId, Id = (Guid?)m.Id } into mj
        from m in mj.DefaultIfEmpty()
        join c in db.Clients.AsNoTracking() on new { e.OfficeId, Id = e.ClientId } equals new { c.OfficeId, Id = (Guid?)c.Id } into cj
        from c in cj.DefaultIfEmpty()
        join u in db.Users.AsNoTracking() on new { e.OfficeId, Id = e.AssigneeId } equals new { u.OfficeId, Id = (Guid?)u.Id } into uj
        from u in uj.DefaultIfEmpty()
        select new CalendarEntry(
            e.Id, e.Category, e.Type, e.Title, e.StartAt, e.EndAt, e.AllDay,
            e.MatterId, m != null ? m.Title : null, e.ClientId, c != null ? c.Name : null,
            e.AssigneeId, u != null ? u.Email : null, e.Location, e.Status,
            e.Result, e.Notes, e.Rrule, e.RemindBeforeMin, e.ReminderStatus, true);
}
