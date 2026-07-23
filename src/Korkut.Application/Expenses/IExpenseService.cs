using Korkut.Application.Expenses.Requests;
using Korkut.Application.Expenses.Responses;

namespace Korkut.Application.Expenses;

/// <summary>Expense use-cases. Missing records raise <c>NotFoundException</c>.</summary>
public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseResponse>> ListAsync(CancellationToken ct = default);

    Task<ExpenseResponse> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
