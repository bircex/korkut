using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Features.Finance;
using Korkut.Domain.Finance;
using Korkut.SharedKernel.Time;

namespace Korkut.Application.Tests;

/// <summary>
/// Unit tests for <see cref="FinanceService"/> — no DB, no EF. The repository ports are
/// in-memory fakes, which is exactly the seam the Clean Architecture refactor buys: the
/// business rules (id/time stamping, the summary roll-up) are exercised in microseconds.
/// </summary>
public class FinanceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeInvoiceRepo : IInvoiceRepository
    {
        public readonly List<Invoice> Items = [];

        public Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default)
        {
            Items.Add(invoice);
            return Task.FromResult(invoice);
        }

        public Task<IReadOnlyList<Invoice>> ListAsync(Guid officeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Invoice>>(Items.Where(i => i.OfficeId == officeId).ToList());

        public Task<Invoice?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.OfficeId == officeId && i.Id == id));

        public Task<bool> UpdateAsync(Guid officeId, Guid id, string? status, decimal? amount, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.OfficeId == officeId && x.Id == id);
            if (i < 0)
            {
                return Task.FromResult(false);
            }

            Items[i] = Items[i] with { Status = status ?? Items[i].Status, Amount = amount ?? Items[i].Amount };
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.RemoveAll(x => x.OfficeId == officeId && x.Id == id) > 0);
    }

    private sealed class FakeExpenseRepo : IExpenseRepository
    {
        public readonly List<Expense> Items = [];

        public Task<Expense> AddAsync(Expense expense, CancellationToken ct = default)
        {
            Items.Add(expense);
            return Task.FromResult(expense);
        }

        public Task<IReadOnlyList<Expense>> ListAsync(Guid officeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Expense>>(Items.Where(e => e.OfficeId == officeId).ToList());

        public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.RemoveAll(x => x.OfficeId == officeId && x.Id == id) > 0);
    }

    [Fact]
    public async Task Create_invoice_stamps_ids_and_derives_gross()
    {
        var svc = new FinanceService(new FakeInvoiceRepo(), new FakeExpenseRepo(), new FixedClock());
        var office = Guid.CreateVersion7();

        var invoice = await svc.CreateInvoiceAsync(office, 1000m, 0.20m, null, null, "iş", "sent", null);

        Assert.NotEqual(Guid.Empty, invoice.Id);
        Assert.Equal(Now, invoice.CreatedAt);
        Assert.Equal(Now, invoice.IssuedAt);         // defaulted from the clock
        Assert.Equal(1200m, invoice.Gross);          // derived
    }

    [Fact]
    public async Task Summary_uses_gross_excludes_cancelled_and_computes_outstanding()
    {
        var svc = new FinanceService(new FakeInvoiceRepo(), new FakeExpenseRepo(), new FixedClock());
        var office = Guid.CreateVersion7();

        await svc.CreateInvoiceAsync(office, 1000m, 0.20m, null, null, null, "paid", null);      // gross 1200
        await svc.CreateInvoiceAsync(office, 500m, 0.20m, null, null, null, "sent", null);       // gross 600
        await svc.CreateInvoiceAsync(office, 100m, 0.20m, null, null, null, "cancelled", null);  // excluded
        await svc.CreateExpenseAsync(office, 250m, null, null, "harç", null, null);

        var t = await svc.SummaryAsync(office);

        Assert.Equal(1800m, t.Invoiced);     // 1200 + 600
        Assert.Equal(1200m, t.Paid);
        Assert.Equal(600m, t.Outstanding);   // 1800 - 1200
        Assert.Equal(250m, t.Expenses);
    }

    [Fact]
    public async Task Patch_missing_invoice_returns_null()
    {
        var svc = new FinanceService(new FakeInvoiceRepo(), new FakeExpenseRepo(), new FixedClock());
        Assert.Null(await svc.UpdateInvoiceAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), "paid", null));
    }
}
