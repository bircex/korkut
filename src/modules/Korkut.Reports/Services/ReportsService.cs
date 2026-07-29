using Korkut.Contract.Clients;
using Korkut.Contract.Correspondence;
using Korkut.Contract.Deadlines;
using Korkut.Contract.Finance;
using Korkut.Contract.Hearings;
using Korkut.Contract.Matters;
using Korkut.Contract.Reports;
using Korkut.Contract.Tasks;
using Korkut.Core;

namespace Korkut.Reports.Services;

/// <summary>
/// Dashboard orchestration. The overview is COMPOSED by calling each domain's Contract lookup —
/// Reports owns no cross-module query and touches no other module's table. Finance is computed
/// from the same invoice/expense figures the finance screens use, so the two can never drift.
/// </summary>
internal sealed class ReportsService(
    IMatterLookup matters,
    IClientLookup clients,
    ITaskLookup tasks,
    IHearingLookup hearings,
    IDeadlineLookup deadlines,
    ICorrespondenceLookup correspondence,
    IFinanceLookup finance,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IReportsService
{
    /// <summary>The agenda window when the caller does not ask for one.</summary>
    public int DefaultWindowDays { get; init; } = 14;

    public Task<ReportsOverviewResponse> OverviewAsync(int? days, CancellationToken ct = default)
    {
        var window = Math.Clamp(days ?? DefaultWindowDays, 1, 365);
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var now = clock.UtcNow;
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var until = today.AddDays(window);

            var status = await matters.StatusCountsAsync(office, ct);
            var clientCount = await clients.CountAsync(office, ct);
            var taskCounts = await tasks.CountsAsync(office, now, ct);
            var hearingsUpcoming = await hearings.CountInRangeAsync(office, now, now.AddDays(window), ct);
            var deadlineCounts = await deadlines.CountsAsync(office, today, until, ct);
            var evrak = await correspondence.DirectionCountsAsync(office, ct);
            var totals = await finance.GetTotalsAsync(office, ct);

            return new ReportsOverviewResponse(
                new MatterStatsResponse(status.Values.Sum(), status),
                new ClientStatsResponse(clientCount),
                new FinanceStatsResponse(totals.Invoiced, totals.Paid, totals.Outstanding, totals.Expenses),
                new TaskStatsResponse(taskCounts.Open, taskCounts.Done, taskCounts.Overdue),
                new AgendaStatsResponse(window, hearingsUpcoming, deadlineCounts.Upcoming, deadlineCounts.Overdue),
                new EvrakStatsResponse(evrak.Values.Sum(), evrak));
        }, ct);
    }
}
