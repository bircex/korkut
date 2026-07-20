using Korkut.Domain.Finance;

namespace Korkut.Application.Features.Finance;

/// <summary>
/// The finance use-cases (invoices, expenses, the office money summary). Depends only on
/// the repository PORTS, so it is unit-testable with fakes and unaware of EF/Postgres.
/// The caller supplies the already-scoped <c>officeId</c> (RLS).
/// </summary>
public interface IFinanceService
{
    Task<Invoice> CreateInvoiceAsync(
        Guid officeId, decimal amount, decimal vatRate, Guid? clientId, Guid? matterId,
        string? description, string status, DateTimeOffset? issuedAt, CancellationToken ct = default);

    Task<IReadOnlyList<Invoice>> ListInvoicesAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Patch an invoice; returns the updated invoice, or null if it does not exist.</summary>
    Task<Invoice?> UpdateInvoiceAsync(Guid officeId, Guid id, string? status, decimal? amount, CancellationToken ct = default);

    Task<bool> DeleteInvoiceAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Expense> CreateExpenseAsync(
        Guid officeId, decimal amount, Guid? matterId, Guid? clientId,
        string? kind, string? description, DateTimeOffset? spentAt, CancellationToken ct = default);

    Task<IReadOnlyList<Expense>> ListExpensesAsync(Guid officeId, CancellationToken ct = default);

    Task<bool> DeleteExpenseAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<FinanceTotals> SummaryAsync(Guid officeId, CancellationToken ct = default);
}
