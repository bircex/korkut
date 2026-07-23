using Korkut.Application.Common;
using Korkut.Application.Errors;
using Korkut.Application.Tasks.Requests;
using Korkut.Application.Tasks.Responses;
using Korkut.Domain.Tasks;
using Korkut.Domain.Time;

namespace Korkut.Application.Tasks;

/// <summary>
/// Task use-cases over the repository port. Owns the completion rule: moving a task to
/// "done" stamps its completion time, moving it back clears it.
/// </summary>
public sealed class TaskService(
    ITaskRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ITaskService
{
    public Task<IReadOnlyList<TaskResponse>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<TaskResponse>)(await repository.ListAsync(office, matterId, ct)).Select(ToResponse).ToList(), ct);

    public Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var task = new WorkTask(office, request.Title!.Trim(), request.MatterId, request.AssigneeId, request.DueAt, clock.UtcNow);
            await repository.AddAsync(task, ct);
            return ToResponse(task);
        }, ct);

    public Task<TaskResponse> SetStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var task = await repository.FindForUpdateAsync(office, id, ct)
                ?? throw new NotFoundException("Görev bulunamadı.");
            task.SetStatus(request.Status!, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return ToResponse(task);
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (!await repository.RemoveAsync(office, id, ct))
            {
                throw new NotFoundException("Görev bulunamadı.");
            }

            return true;
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static TaskResponse ToResponse(WorkTask t) =>
        new(t.Id, t.Title, t.Status, t.MatterId, t.AssigneeId, t.DueAt, t.CompletedAt, t.CreatedAt);
}
