using Korkut.Finance.Repositories;
using Korkut.Contract;
using Korkut.Contract.Finance;
using Korkut.Core;
using Korkut.Finance.Entities;

namespace Korkut.Finance.Services;

/// <summary>
/// One service for everything finance: invoice + expense use-cases and the office money roll-up.
/// Every call runs in the caller's tenant scope and RETURNS a <see cref="Result"/>.
/// </summary>
internal sealed class FinanceService(
    IInvoiceRepository invoices, IExpenseRepository expenses,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IFinanceService, IFinanceLookup
{
    // ---- Invoices ----
    public Task<Result<IReadOnlyList<InvoiceResponse>>> ListInvoicesAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<InvoiceResponse>>.Ok((await invoices.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<InvoiceResponse>> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var invoice = new Invoice(
                office, request.Amount, request.VatRate, request.ClientId, request.MatterId,
                request.Description, request.Status, request.IssuedAt ?? now, now);
            await invoices.AddAsync(invoice, ct);
            return Result<InvoiceResponse>.Ok(ToResponse(invoice));
        }, ct);

    public Task<Result<InvoiceResponse>> UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var invoice = await invoices.FindForUpdateAsync(office, id, ct);
            if (invoice is null)
            {
                return Result<InvoiceResponse>.NotFound("Fatura bulunamadı.");
            }

            invoice.Update(request.Status, request.Amount);
            await invoices.SaveChangesAsync(ct);
            return Result<InvoiceResponse>.Ok(ToResponse(invoice));
        }, ct);

    public Task<Result> DeleteInvoiceAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await invoices.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Fatura bulunamadı."), ct);

    // ---- Expenses ----
    public Task<Result<IReadOnlyList<ExpenseResponse>>> ListExpensesAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<ExpenseResponse>>.Ok((await expenses.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<ExpenseResponse>> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var expense = new Expense(
                office, request.Amount, request.MatterId, request.ClientId,
                request.Kind, request.Description, request.SpentAt ?? now, now);
            await expenses.AddAsync(expense, ct);
            return Result<ExpenseResponse>.Ok(ToResponse(expense));
        }, ct);

    public Task<Result> DeleteExpenseAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await expenses.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Masraf bulunamadı."), ct);

    // ---- Roll-up ----
    public Task<Result<FinanceSummaryResponse>> GetSummaryAsync(CancellationToken ct = default)
        => InScopeAsync(async office => Result<FinanceSummaryResponse>.Ok(await TotalsAsync(office, ct)), ct);

    /// <summary>Cross-module read used by the reports dashboard, in the caller's already-open scope.</summary>
    public Task<FinanceSummaryResponse> GetTotalsAsync(Guid officeId, CancellationToken ct = default)
        => TotalsAsync(officeId, ct);

    private async Task<FinanceSummaryResponse> TotalsAsync(Guid officeId, CancellationToken ct)
        => FinanceTotalsCalculator.Compute(await invoices.ListAsync(officeId, ct), await expenses.ListAsync(officeId, ct));

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static InvoiceResponse ToResponse(Invoice i) =>
        new(i.Id, i.ClientId, i.MatterId, i.Amount, i.VatRate, i.Gross, i.Description, i.Status, i.IssuedAt);

    private static ExpenseResponse ToResponse(Expense e) =>
        new(e.Id, e.MatterId, e.ClientId, e.Amount, e.Kind, e.Description, e.SpentAt);
}
