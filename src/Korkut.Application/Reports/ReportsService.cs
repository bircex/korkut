using Korkut.Core;
using Korkut.Application.Expenses;
using Korkut.Application.FinanceSummary;
using Korkut.Application.Invoices;
using Korkut.Application.Reports.Responses;

namespace Korkut.Application.Reports;

/// <summary>
/// Dashboard orchestration over the aggregation port. Finance is not queried separately
/// here: it is computed from the same invoice and expense figures the finance screens use,
/// through <see cref="FinanceTotalsCalculator"/>, so the two can never drift apart.
/// </summary>
public sealed class ReportsService(
    IReportsQuery query,
    IInvoiceRepository invoices,
    IExpenseRepository expenses,
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
            var stats = await query.GetStatsAsync(office, window, clock.UtcNow, ct);
            var totals = FinanceTotalsCalculator.Compute(
                await invoices.ListAsync(office, ct), await expenses.ListAsync(office, ct));

            return new ReportsOverviewResponse(
                new MatterStatsResponse(stats.Matters.Total, stats.Matters.ByStatus),
                new ClientStatsResponse(stats.Clients),
                new FinanceStatsResponse(totals.Invoiced, totals.Paid, totals.Outstanding, totals.Expenses),
                new TaskStatsResponse(stats.Tasks.Open, stats.Tasks.Done, stats.Tasks.Overdue),
                new AgendaStatsResponse(
                    stats.Agenda.Days, stats.Agenda.HearingsUpcoming,
                    stats.Agenda.DeadlinesUpcoming, stats.Agenda.DeadlinesOverdue),
                new EvrakStatsResponse(stats.Evrak.Total, stats.Evrak.ByDirection));
        }, ct);
    }
}
