using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Features.Finance;
using Korkut.Domain.Reports;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Reports;

/// <summary>
/// Office dashboard use-case: rolls up the non-finance stats (repository) and the finance
/// totals (<see cref="IFinanceService"/>, so the derived gross stays consistent) into one
/// overview.
/// </summary>
public interface IReportsService
{
    Task<ReportsOverview> OverviewAsync(Guid officeId, int days, CancellationToken ct = default);
}

/// <summary>Reports orchestration over the aggregation port + the finance service.</summary>
public sealed class ReportsService(IReportsRepository repo, IFinanceService finance, IClock clock) : IReportsService
{
    public async Task<ReportsOverview> OverviewAsync(Guid officeId, int days, CancellationToken ct = default)
    {
        var stats = await repo.GetStatsAsync(officeId, days, clock.UtcNow, ct);
        var financeTotals = await finance.SummaryAsync(officeId, ct);
        return new ReportsOverview(stats.Matters, stats.Clients, financeTotals, stats.Tasks, stats.Agenda, stats.Evrak);
    }
}
