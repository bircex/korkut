using Korkut.Contract;
using Korkut.Contract.Expenses;
using Korkut.Core;

namespace Korkut.Expenses;

/// <summary>Expense use-cases over the repository port; an omitted spend date defaults to now. Returns Result.</summary>
public sealed class ExpenseService(
    IExpenseRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IExpenseService
{
    public Task<Result<IReadOnlyList<ExpenseResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<ExpenseResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<ExpenseResponse>> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var expense = new Expense(
                office, request.Amount, request.MatterId, request.ClientId,
                request.Kind, request.Description, request.SpentAt ?? now, now);
            await repository.AddAsync(expense, ct);
            return Result<ExpenseResponse>.Ok(ToResponse(expense));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Masraf bulunamadı."), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static ExpenseResponse ToResponse(Expense e) =>
        new(e.Id, e.MatterId, e.ClientId, e.Amount, e.Kind, e.Description, e.SpentAt);
}
