using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

public sealed record MatterStats(int Total, IReadOnlyDictionary<string, int> ByStatus);

public sealed record TaskStats(int Open, int Done, int Overdue);

public sealed record AgendaStats(int Days, int HearingsUpcoming, int DeadlinesUpcoming, int DeadlinesOverdue);

public sealed record EvrakStats(int Total, IReadOnlyDictionary<string, int> ByDirection);

public sealed record ReportsOverview(
    MatterStats Matters, int Clients, FinanceTotals Finance, TaskStats Tasks, AgendaStats Agenda, EvrakStats Evrak);

/// <summary>
/// Read-only office dashboard: counts and roll-ups across matters, clients, finance,
/// tasks, the upcoming agenda, and the correspondence register. Tenant context is set
/// by the caller (RLS), so every aggregate is scoped to the office. The finance block
/// is delegated to <see cref="FinanceStore"/> so the derived gross stays consistent.
/// </summary>
public sealed class ReportsStore(KorkutDbContext db, FinanceStore finance, IClock clock)
{
    public async Task<ReportsOverview> OverviewAsync(Guid officeId, int days, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var until = today.AddDays(days);
        var untilInstant = now.AddDays(days);

        // Matters (exclude soft-deleted) + status breakdown.
        var matterRows = await db.Matters.AsNoTracking()
            .Where(m => m.OfficeId == officeId && m.DeletedAt == null)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = matterRows.ToDictionary(r => r.Status, r => r.Count);
        var matters = new MatterStats(matterRows.Sum(r => r.Count), byStatus);

        var clients = await db.Clients.AsNoTracking().CountAsync(c => c.OfficeId == officeId && c.DeletedAt == null, ct);

        var financeTotals = await finance.SummaryAsync(officeId, ct);

        // Tasks: open (not done/cancelled), done, overdue (past due + still open).
        var open = await db.Tasks.CountAsync(t => t.OfficeId == officeId && t.Status != "done" && t.Status != "cancelled", ct);
        var done = await db.Tasks.CountAsync(t => t.OfficeId == officeId && t.Status == "done", ct);
        var overdue = await db.Tasks.CountAsync(t => t.OfficeId == officeId
            && t.Status != "done" && t.Status != "cancelled" && t.DueAt != null && t.DueAt < now, ct);
        var tasks = new TaskStats(open, done, overdue);

        // Agenda within the look-ahead window.
        var hearingsUpcoming = await db.MatterHearings.CountAsync(h => h.OfficeId == officeId && h.ScheduledAt >= now && h.ScheduledAt <= untilInstant, ct);
        var deadlinesUpcoming = await db.Deadlines.CountAsync(d => d.OfficeId == officeId && d.CompletedAt == null && d.DueDate >= today && d.DueDate <= until, ct);
        var deadlinesOverdue = await db.Deadlines.CountAsync(d => d.OfficeId == officeId && d.CompletedAt == null && d.DueDate < today, ct);
        var agenda = new AgendaStats(days, hearingsUpcoming, deadlinesUpcoming, deadlinesOverdue);

        // Evrak total + direction breakdown.
        var evrakRows = await db.Correspondence.AsNoTracking()
            .Where(e => e.OfficeId == officeId)
            .GroupBy(e => e.Direction)
            .Select(g => new { Direction = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var evrak = new EvrakStats(evrakRows.Sum(r => r.Count), evrakRows.ToDictionary(r => r.Direction, r => r.Count));

        return new ReportsOverview(matters, clients, financeTotals, tasks, agenda, evrak);
    }
}
