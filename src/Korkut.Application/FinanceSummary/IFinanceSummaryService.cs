using Korkut.Application.FinanceSummary.Responses;

namespace Korkut.Application.FinanceSummary;

/// <summary>The office's financial roll-up across invoices and expenses (read-only).</summary>
public interface IFinanceSummaryService
{
    Task<FinanceSummaryResponse> GetAsync(CancellationToken ct = default);
}
