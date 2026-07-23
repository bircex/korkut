using Korkut.Application.Common;
using Korkut.Application.Errors;
using Korkut.Application.Expenses.Requests;
using Korkut.Application.Expenses.Responses;
using Korkut.Domain.Expenses;
using Korkut.Domain.Time;

namespace Korkut.Application.Expenses;

/// <summary>Expense use-cases over the repository port; an omitted spend date defaults to now.</summary>
public sealed class ExpenseService(
    IExpenseRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IExpenseService
{
    public Task<IReadOnlyList<ExpenseResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<ExpenseResponse>)(await repository.ListAsync(office, ct)).Select(ToResponse).ToList(), ct);

    public Task<ExpenseResponse> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var expense = new Expense(
                office, request.Amount, request.MatterId, request.ClientId,
                request.Kind, request.Description, request.SpentAt ?? now, now);
            await repository.AddAsync(expense, ct);
            return ToResponse(expense);
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (!await repository.RemoveAsync(office, id, ct))
            {
                throw new NotFoundException("Masraf bulunamadı.");
            }

            return true;
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static ExpenseResponse ToResponse(Expense e) =>
        new(e.Id, e.MatterId, e.ClientId, e.Amount, e.Kind, e.Description, e.SpentAt);
}
