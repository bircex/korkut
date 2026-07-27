using Korkut.Contract.Finance;

namespace Korkut.Finance;

/// <summary>
/// The single definition of the office money roll-up, shared by the finance summary and the reports
/// dashboard so the two can never disagree: cancelled invoices are excluded, totals use the
/// VAT-inclusive gross, and outstanding is invoiced minus paid.
/// </summary>
public static class FinanceTotalsCalculator
{
    public static FinanceSummaryResponse Compute(IEnumerable<Invoice> invoices, IEnumerable<Expense> expenses)
    {
        var active = invoices.Where(i => !i.IsCancelled).ToList();
        var invoiced = active.Sum(i => i.Gross);
        var paid = active.Where(i => i.IsPaid).Sum(i => i.Gross);
        return new FinanceSummaryResponse(invoiced, paid, invoiced - paid, expenses.Sum(e => e.Amount));
    }
}
