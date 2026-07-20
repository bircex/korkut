using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>A unified calendar row (an editable calendar_events row, or a read-only hearing/deadline).</summary>
public sealed record CalendarRow(
    Guid Id, string Category, string? Type, string Title,
    DateTimeOffset StartAt, DateTimeOffset? EndAt, bool AllDay,
    Guid? MatterId, string? MatterTitle, Guid? ClientId, string? ClientName,
    Guid? AssigneeId, string? AssigneeName, string? Location, string Status,
    string? Result, string? Notes, string? Rrule, int? RemindBeforeMin, string ReminderStatus,
    bool Editable);

/// <summary>An upcoming-agenda item (hearing or deadline) within the look-ahead window.</summary>
public sealed record AgendaRow(string Kind, DateTimeOffset Date, Guid Id, Guid? MatterId, string Title, string? Status);

/// <summary>
/// Calendar CRUD (on <c>calendar_events</c>) plus two read views that merge the
/// office's real hearings and deadlines: the unified event list (GET /events) and the
/// upcoming agenda (GET /upcoming). Tenant context is set by the caller (RLS). Only
/// calendar_events rows are mutated here; hearings/deadlines are owned by their domains.
/// </summary>
public sealed class CalendarStore(KorkutDbContext db, IClock clock)
{
    private static DateTimeOffset AtMidnightUtc(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    // --- CRUD (calendar_events only) ---

    public async Task<Guid> CreateAsync(CalendarEventEntity e, CancellationToken ct = default)
    {
        e.Id = UuidV7.NewId();
        e.CreatedAt = clock.UtcNow;
        db.CalendarEvents.Add(e);
        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public Task<CalendarEventEntity?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => db.CalendarEvents.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);

    /// <summary>Partial update of an editable event. Null fields are left unchanged. Returns false if missing.</summary>
    public async Task<bool> UpdateAsync(
        Guid officeId, Guid id, string? title, string? type, DateTimeOffset? startAt, DateTimeOffset? endAt,
        bool? allDay, Guid? matterId, Guid? clientId, Guid? assigneeId, string? location, string? status,
        string? result, string? notes, string? rrule, int? remindBeforeMin, CancellationToken ct = default)
    {
        var affected = await db.CalendarEvents
            .Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Title, x => title ?? x.Title)
                .SetProperty(x => x.Type, x => type ?? x.Type)
                .SetProperty(x => x.StartAt, x => startAt ?? x.StartAt)
                .SetProperty(x => x.EndAt, x => endAt ?? x.EndAt)
                .SetProperty(x => x.AllDay, x => allDay ?? x.AllDay)
                .SetProperty(x => x.MatterId, x => matterId ?? x.MatterId)
                .SetProperty(x => x.ClientId, x => clientId ?? x.ClientId)
                .SetProperty(x => x.AssigneeId, x => assigneeId ?? x.AssigneeId)
                .SetProperty(x => x.Location, x => location ?? x.Location)
                .SetProperty(x => x.Status, x => status ?? x.Status)
                .SetProperty(x => x.Result, x => result ?? x.Result)
                .SetProperty(x => x.Notes, x => notes ?? x.Notes)
                .SetProperty(x => x.Rrule, x => rrule ?? x.Rrule)
                .SetProperty(x => x.RemindBeforeMin, x => remindBeforeMin ?? x.RemindBeforeMin), ct);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.CalendarEvents.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    // --- Unified event list (calendar_events ∪ deadlines ∪ hearings) ---

    public async Task<List<CalendarRow>> ListEventsAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, string? category, Guid? matterId, CancellationToken ct = default)
    {
        var rows = new List<CalendarRow>();

        // Editable calendar_events, enriched with matter/client/assignee display fields.
        if (category is null or "appointment" or "reminder" or "hearing" or "deadline")
        {
            var events = await (
                from e in db.CalendarEvents.AsNoTracking()
                where e.OfficeId == officeId
                    && (start == null || e.StartAt >= start)
                    && (end == null || e.StartAt <= end)
                    && (category == null || e.Category == category)
                    && (matterId == null || e.MatterId == matterId)
                join m in db.Matters.AsNoTracking() on new { e.OfficeId, Id = e.MatterId } equals new { m.OfficeId, Id = (Guid?)m.Id } into mj
                from m in mj.DefaultIfEmpty()
                join c in db.Clients.AsNoTracking() on new { e.OfficeId, Id = e.ClientId } equals new { c.OfficeId, Id = (Guid?)c.Id } into cj
                from c in cj.DefaultIfEmpty()
                join u in db.Users.AsNoTracking() on new { e.OfficeId, Id = e.AssigneeId } equals new { u.OfficeId, Id = (Guid?)u.Id } into uj
                from u in uj.DefaultIfEmpty()
                select new CalendarRow(
                    e.Id, e.Category, e.Type, e.Title, e.StartAt, e.EndAt, e.AllDay,
                    e.MatterId, m != null ? m.Title : null, e.ClientId, c != null ? c.Name : null,
                    e.AssigneeId, u != null ? u.Email : null, e.Location, e.Status,
                    e.Result, e.Notes, e.Rrule, e.RemindBeforeMin, e.ReminderStatus, true))
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
                .Select(d => new CalendarRow(
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
                select new CalendarRow(
                    h.Id, "hearing", null, m.Title, h.ScheduledAt, null, false,
                    h.MatterId, m.Title, null, null, null, null, null, "scheduled", null, null, null, null, "pending", false))
                .ToListAsync(ct);
            rows.AddRange(hearings);
        }

        return rows.OrderBy(r => r.StartAt).ToList();
    }

    // --- Upcoming agenda (hearings + deadlines within N days) ---

    public async Task<List<AgendaRow>> AgendaAsync(Guid officeId, int days, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var until = now.AddDays(days);

        var hearings = await (
            from h in db.MatterHearings.AsNoTracking()
            where h.OfficeId == officeId && h.ScheduledAt >= now && h.ScheduledAt <= until
            join m in db.Matters.AsNoTracking() on new { h.OfficeId, h.MatterId } equals new { m.OfficeId, MatterId = m.Id }
            select new AgendaRow("hearing", h.ScheduledAt, h.Id, h.MatterId, m.Title, "scheduled"))
            .ToListAsync(ct);

        var deadlines = await db.Deadlines.AsNoTracking()
            .Where(d => d.OfficeId == officeId && d.CompletedAt == null)
            .Select(d => new { d.Id, d.Title, d.DueDate, d.MatterId })
            .ToListAsync(ct);

        var deadlineRows = deadlines
            .Select(d => new AgendaRow("deadline", AtMidnightUtc(d.DueDate), d.Id, d.MatterId, d.Title, "open"))
            .Where(r => r.Date >= now.AddDays(-1) && r.Date <= until); // include today's/just-overdue deadlines

        return hearings.Concat(deadlineRows).OrderBy(r => r.Date).ToList();
    }
}
