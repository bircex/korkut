using Korkut.Application.Common;
using Korkut.Application.Expenses;
using Korkut.Application.FinanceSummary.Responses;
using Korkut.Application.Invoices;

namespace Korkut.Application.FinanceSummary;

/// <summary>
/// A read model composed from the invoice and expense aggregates: cancelled invoices are
/// excluded, totals use the VAT-inclusive gross, and outstanding is invoiced minus paid.
/// </summary>
public sealed class FinanceSummaryService(
    IInvoiceRepository invoices, IExpenseRepository expenses, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    : IFinanceSummaryService
{
    public Task<FinanceSummaryResponse> GetAsync(CancellationToken ct = default)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var totals = FinanceTotalsCalculator.Compute(
                await invoices.ListAsync(office, ct), await expenses.ListAsync(office, ct));
            return new FinanceSummaryResponse(totals.Invoiced, totals.Paid, totals.Outstanding, totals.Expenses);
        }, ct);
    }
}
