using Korkut.Contract;
using Korkut.Contract.Tasks;
using Korkut.Core;

namespace Korkut.Tasks;

/// <summary>
/// Task use-cases over the repository port. Owns the completion rule: moving a task to "done"
/// stamps its completion time, moving it back clears it. Every call RETURNS a <see cref="Result"/>.
/// </summary>
public sealed class TaskService(
    ITaskRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ITaskService
{
    public Task<Result<IReadOnlyList<TaskResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<TaskResponse>>.Ok((await repository.ListAsync(office, matterId, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<TaskResponse>> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var task = new WorkTask(office, request.Title!.Trim(), request.MatterId, request.AssigneeId, request.DueAt, clock.UtcNow);
            await repository.AddAsync(task, ct);
            return Result<TaskResponse>.Ok(ToResponse(task));
        }, ct);

    public Task<Result<TaskResponse>> SetStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var task = await repository.FindForUpdateAsync(office, id, ct);
            if (task is null)
            {
                return Result<TaskResponse>.NotFound("Görev bulunamadı.");
            }

            task.SetStatus(request.Status!, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return Result<TaskResponse>.Ok(ToResponse(task));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct)
                ? Result.Ok()
                : Result.NotFound("Görev bulunamadı."), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static TaskResponse ToResponse(WorkTask t) =>
        new(t.Id, t.Title, t.Status, t.MatterId, t.AssigneeId, t.DueAt, t.CompletedAt, t.CreatedAt);
}
