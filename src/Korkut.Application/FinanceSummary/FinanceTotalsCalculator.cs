using Korkut.Expenses;
using Korkut.Invoices;

namespace Korkut.Application.FinanceSummary;

/// <summary>
/// The single definition of the office money roll-up, shared by the finance summary and the
/// reports dashboard so the two can never disagree: cancelled invoices are excluded, totals
/// use the VAT-inclusive gross, and outstanding is invoiced minus paid.
/// </summary>
public static class FinanceTotalsCalculator
{
    public static (decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses) Compute(
        IEnumerable<Invoice> invoices, IEnumerable<Expense> expenses)
    {
        var active = invoices.Where(i => !i.IsCancelled).ToList();
        var invoiced = active.Sum(i => i.Gross);
        var paid = active.Where(i => i.IsPaid).Sum(i => i.Gross);
        return (invoiced, paid, invoiced - paid, expenses.Sum(e => e.Amount));
    }
}
