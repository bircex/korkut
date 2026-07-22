using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Finance;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Finance;

/// <summary>
/// Finance orchestration over the repository ports. Owns the business rules — id/time
/// stamping, the summary roll-up (gross via <see cref="Invoice.Gross"/>, cancelled
/// excluded, outstanding = invoiced − paid) — with no knowledge of how data is stored.
/// </summary>
public sealed class FinanceService(IInvoiceRepository invoices, IExpenseRepository expenses, IClock clock) : IFinanceService
{
    public Task<Invoice> CreateInvoiceAsync(
        Guid officeId, decimal amount, decimal vatRate, Guid? clientId, Guid? matterId,
        string? description, string status, DateTimeOffset? issuedAt, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var invoice = new Invoice(UuidV7.NewId(), officeId, clientId, matterId, amount, vatRate,
            description, status, issuedAt ?? now, now);
        return invoices.AddAsync(invoice, ct);
    }

    public Task<IReadOnlyList<Invoice>> ListInvoicesAsync(Guid officeId, CancellationToken ct = default)
        => invoices.ListAsync(officeId, ct);

    public async Task<Invoice?> UpdateInvoiceAsync(Guid officeId, Guid id, string? status, decimal? amount, CancellationToken ct = default)
        => await invoices.UpdateAsync(officeId, id, status, amount, ct) ? await invoices.GetAsync(officeId, id, ct) : null;

    public Task<bool> DeleteInvoiceAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => invoices.DeleteAsync(officeId, id, ct);

    public Task<Expense> CreateExpenseAsync(
        Guid officeId, decimal amount, Guid? matterId, Guid? clientId,
        string? kind, string? description, DateTimeOffset? spentAt, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var expense = new Expense(UuidV7.NewId(), officeId, matterId, clientId, amount, kind, description, spentAt ?? now, now);
        return expenses.AddAsync(expense, ct);
    }

    public Task<IReadOnlyList<Expense>> ListExpensesAsync(Guid officeId, CancellationToken ct = default)
        => expenses.ListAsync(officeId, ct);

    public Task<bool> DeleteExpenseAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => expenses.DeleteAsync(officeId, id, ct);

    public async Task<FinanceTotals> SummaryAsync(Guid officeId, CancellationToken ct = default)
    {
        var all = await invoices.ListAsync(officeId, ct);
        var active = all.Where(i => i.Status != "cancelled").ToList();
        var invoiced = active.Sum(i => i.Gross);
        var paid = active.Where(i => i.Status == "paid").Sum(i => i.Gross);
        var expenseTotal = (await expenses.ListAsync(officeId, ct)).Sum(e => e.Amount);
        return new FinanceTotals(invoiced, paid, invoiced - paid, expenseTotal);
    }
}
