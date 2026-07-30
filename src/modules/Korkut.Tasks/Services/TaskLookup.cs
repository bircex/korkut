using Korkut.Tasks.Entities;
using Korkut.Contract.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Tasks.Services;

/// <summary>
/// Implements the cross-module <see cref="ITaskLookup"/> over the module's own table, reading in the
/// caller's already-open tenant scope. Lets the dashboard tally to-dos and the calendar compose its
/// unified view without ever touching the WorkTask type.
/// </summary>
internal sealed class TaskLookup(DbContext db) : ITaskLookup
{
    public async Task<TaskCounts> CountsAsync(Guid officeId, DateTimeOffset now, CancellationToken ct = default)
    {
        // Only görev-category records are "tasks" for the dashboard; hatırlatma/randevu are not to-dos.
        var scoped = db.Set<WorkTask>().AsNoTracking()
            .Where(t => t.OfficeId == officeId && t.Category == "gorev");
        var open = await scoped.CountAsync(t => t.Status != "done" && t.Status != "cancelled", ct);
        var done = await scoped.CountAsync(t => t.Status == "done", ct);
        var overdue = await scoped.CountAsync(
            t => t.Status != "done" && t.Status != "cancelled" && t.DueAt != null && t.DueAt < now, ct);
        return new TaskCounts(open, done, overdue);
    }

    public async Task<IReadOnlyList<TaskCalendarRow>> ListInRangeAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, Guid? matterId, CancellationToken ct = default)
        => await db.Set<WorkTask>().AsNoTracking()
            .Where(t => t.OfficeId == officeId && t.DueAt != null)
            .Where(t => matterId == null || t.MatterId == matterId)
            .Where(t => start == null || t.DueAt >= start)
            .Where(t => end == null || t.DueAt <= end)
            .OrderBy(t => t.DueAt)
            .Select(t => new TaskCalendarRow(
                t.Id, t.Category, t.Type, t.Title, t.DueAt!.Value, t.EndAt, t.AllDay,
                t.MatterId, t.ClientId, t.AssigneeId, t.Location, t.Status, t.Description,
                t.Rrule, t.RemindBeforeMin, t.ReminderStatus))
            .ToListAsync(ct);
}
