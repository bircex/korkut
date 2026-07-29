using Korkut.Tasks.Entities;
using Korkut.Contract.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Tasks.Services;

/// <summary>
/// Implements the cross-module <see cref="ITaskLookup"/> over the module's own table, reading in the
/// caller's already-open tenant scope. Lets the dashboard tally tasks without the WorkTask type.
/// </summary>
internal sealed class TaskLookup(DbContext db) : ITaskLookup
{
    public async Task<TaskCounts> CountsAsync(Guid officeId, DateTimeOffset now, CancellationToken ct = default)
    {
        var scoped = db.Set<WorkTask>().AsNoTracking().Where(t => t.OfficeId == officeId);
        var open = await scoped.CountAsync(t => t.Status != "done" && t.Status != "cancelled", ct);
        var done = await scoped.CountAsync(t => t.Status == "done", ct);
        var overdue = await scoped.CountAsync(
            t => t.Status != "done" && t.Status != "cancelled" && t.DueAt != null && t.DueAt < now, ct);
        return new TaskCounts(open, done, overdue);
    }
}
