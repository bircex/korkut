namespace Korkut.Contract.Finance;

/// <summary>The office's money roll-up: invoiced and paid are VAT-inclusive.</summary>
public sealed record FinanceSummaryResponse(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);

/// <summary>
/// One service for everything finance: invoices, expenses and the office money roll-up. The
/// summary is derived from the same invoice/expense figures, so the screens can never drift.
/// Every call RETURNS a <see cref="Result"/>.
/// </summary>
public interface IFinanceService
{
    // Invoices
    Task<Result<IReadOnlyList<InvoiceResponse>>> ListInvoicesAsync(CancellationToken ct = default);
    Task<Result<InvoiceResponse>> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct = default);
    Task<Result<InvoiceResponse>> UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default);
    Task<Result> DeleteInvoiceAsync(Guid id, CancellationToken ct = default);

    // Expenses
    Task<Result<IReadOnlyList<ExpenseResponse>>> ListExpensesAsync(CancellationToken ct = default);
    Task<Result<ExpenseResponse>> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken ct = default);
    Task<Result> DeleteExpenseAsync(Guid id, CancellationToken ct = default);

    // Roll-up
    Task<Result<FinanceSummaryResponse>> GetSummaryAsync(CancellationToken ct = default);
}

/// <summary>
/// Cross-module read of the office money totals, in the caller's already-open tenant scope (used by
/// the reports dashboard so it never opens a nested scope). Same figures as the finance summary.
/// </summary>
public interface IFinanceLookup
{
    Task<FinanceSummaryResponse> GetTotalsAsync(Guid officeId, CancellationToken ct = default);
}
