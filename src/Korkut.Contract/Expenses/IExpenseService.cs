namespace Korkut.Contract.Expenses;

/// <summary>Expense use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IExpenseService
{
    Task<Result<IReadOnlyList<ExpenseResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result<ExpenseResponse>> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
