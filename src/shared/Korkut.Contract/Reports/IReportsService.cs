using Korkut.Contract.Reports;

namespace Korkut.Contract.Reports;

/// <summary>
/// The read-only office dashboard: the non-finance roll-ups and the finance totals in one
/// answer. The window is given in days and clamped here, so no caller can ask for a report
/// spanning a decade.
/// </summary>
public interface IReportsService
{
    Task<ReportsOverviewResponse> OverviewAsync(int? days, CancellationToken ct = default);
}
