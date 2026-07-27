using Korkut.Application.FinanceSummary;
using Korkut.Expenses;
using Korkut.Invoices;

namespace Korkut.Application.Tests;

/// <summary>
/// Unit tests for the money rules — no DB, no EF. The VAT-inclusive gross lives on the
/// <see cref="Invoice"/> entity and the roll-up in <see cref="FinanceTotalsCalculator"/>,
/// so both are exercised in microseconds.
/// </summary>
public class FinanceTotalsTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    private static Invoice Invoice(decimal amount, string status) =>
        new(Guid.Empty, amount, 0.20m, null, null, null, status, Now, Now);

    [Fact]
    public void Gross_applies_the_vat_rate()
    {
        Assert.Equal(1200m, Invoice(1000m, "sent").Gross);
    }

    [Fact]
    public void Totals_use_gross_exclude_cancelled_and_compute_outstanding()
    {
        List<Invoice> invoices =
        [
            Invoice(1000m, "paid"),       // gross 1200
            Invoice(500m, "sent"),        // gross 600
            Invoice(100m, "cancelled"),   // excluded
        ];
        List<Expense> expenses = [new(Guid.Empty, 250m, null, null, "harç", null, Now, Now)];

        var totals = FinanceTotalsCalculator.Compute(invoices, expenses);

        Assert.Equal(1800m, totals.Invoiced);     // 1200 + 600
        Assert.Equal(1200m, totals.Paid);
        Assert.Equal(600m, totals.Outstanding);   // 1800 - 1200
        Assert.Equal(250m, totals.Expenses);
    }

    [Fact]
    public void Update_applies_only_the_supplied_fields()
    {
        var invoice = Invoice(1000m, "draft");

        invoice.Update("paid", null);

        Assert.Equal("paid", invoice.Status);
        Assert.Equal(1000m, invoice.Amount);      // untouched
    }
}
