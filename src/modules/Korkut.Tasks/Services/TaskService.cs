using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Contract.Tasks;
using Korkut.Core;
using Korkut.Tasks.Entities;
using Korkut.Tasks.Repositories;

namespace Korkut.Tasks.Services;

/// <summary>
/// Task use-cases over the repository port. Owns the completion rule: moving a task to "done"
/// stamps its completion time, moving it back clears it. Assignee names are resolved through the
/// Organization lookup. Every call RETURNS a <see cref="Result"/>.
/// </summary>
internal sealed class TaskService(
    ITaskRepository repository, IUserLookup users, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ITaskService
{
    private static readonly IReadOnlyDictionary<Guid, string> NoNames = new Dictionary<Guid, string>();

    public Task<Result<IReadOnlyList<TaskResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var tasks = await repository.ListAsync(office, matterId, ct);
            var names = await ResolveAsync(office, tasks.Select(t => t.AssigneeId), ct);
            return Result<IReadOnlyList<TaskResponse>>.Ok(tasks.Select(t => ToResponse(t, names)).ToList());
        }, ct);

    public Task<Result<TaskResponse>> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var task = new WorkTask(office, request.Title!.Trim(), request.MatterId, request.AssigneeId, request.DueDate, clock.UtcNow);
            await repository.AddAsync(task, ct);
            return Result<TaskResponse>.Ok(ToResponse(task, await ResolveAsync(office, [task.AssigneeId], ct)));
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
            return Result<TaskResponse>.Ok(ToResponse(task, await ResolveAsync(office, [task.AssigneeId], ct)));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct)
                ? Result.Ok()
                : Result.NotFound("Görev bulunamadı."), ct);

    private Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(Guid office, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return list.Count == 0 ? Task.FromResult(NoNames) : users.EmailsAsync(office, list, ct);
    }

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static TaskResponse ToResponse(WorkTask t, IReadOnlyDictionary<Guid, string> names) =>
        new(t.Id, t.Title, t.Status, t.MatterId, t.AssigneeId,
            t.AssigneeId is { } a && names.TryGetValue(a, out var n) ? n : null,
            t.DueAt, t.CompletedAt, t.CreatedAt);
}
