using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Contract.Tasks;
using Korkut.Contract.Matters;
using Korkut.Core;
using Korkut.Tasks.Entities;
using Korkut.Tasks.Repositories;

namespace Korkut.Tasks.Services;

/// <summary>
/// Agenda-record use-cases over the repository port (görev / hatırlatma / randevu). Owns the
/// completion rule: moving a record to "done" stamps its completion time, moving it back clears it.
/// Assignee names are resolved through the Organization lookup. Every call RETURNS a
/// <see cref="Result"/>.
/// </summary>
internal sealed class TaskService(
    ITaskRepository repository, IUserLookup users, IMatterAccess access, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ITaskService
{
    private static readonly IReadOnlyDictionary<Guid, string> NoNames = new Dictionary<Guid, string>();

    public Task<Result<IReadOnlyList<TaskResponse>>> ListAsync(
        Guid? matterId, string? status = null, string? category = null, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var scope = await access.ScopeAsync(office, ct);
            var tasks = (await repository.ListAsync(office, matterId, status, category, ct))
                .Where(t => scope.CanSee(t.MatterId, t.AssigneeId, t.CreatedBy)).ToList();
            var names = await ResolveAsync(office, tasks.Select(t => t.AssigneeId), ct);
            return Result<IReadOnlyList<TaskResponse>>.Ok(tasks.Select(t => ToResponse(t, names)).ToList());
        }, ct);

    public Task<Result<TaskResponse>> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (request.MatterId is { } matterId && !(await access.ScopeAsync(office, ct)).CanSee(matterId))
            {
                return Result<TaskResponse>.NotFound("Dosya bulunamadı.");
            }

            var task = new WorkTask(
                office,
                string.IsNullOrWhiteSpace(request.Category) ? "gorev" : request.Category,
                request.Title!.Trim(),
                string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                string.IsNullOrWhiteSpace(request.Type) ? null : request.Type.Trim(),
                request.MatterId, request.AssigneeId, request.ClientId,
                request.DueDate, request.EndAt, request.AllDay,
                string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
                string.IsNullOrWhiteSpace(request.Rrule) ? null : request.Rrule.Trim(),
                request.RemindBeforeMin, clock.UtcNow, currentUser.UserId);
            await repository.AddAsync(task, ct);
            return Result<TaskResponse>.Ok(ToResponse(task, await ResolveAsync(office, [task.AssigneeId], ct)));
        }, ct);

    public Task<Result<TaskResponse>> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var scope = await access.ScopeAsync(office, ct);
            var task = await repository.FindForUpdateAsync(office, id, ct);
            if (task is null || !scope.CanSee(task.MatterId, task.AssigneeId, task.CreatedBy))
            {
                return Result<TaskResponse>.NotFound("Ajanda kaydı bulunamadı.");
            }

            if (request.MatterId is { } matterId && !scope.CanSee(matterId))
            {
                return Result<TaskResponse>.NotFound("Dosya bulunamadı.");
            }

            task.Update(
                request.Category, request.Title?.Trim(), request.Description?.Trim(), request.Type?.Trim(),
                request.MatterId, request.AssigneeId, request.ClientId,
                request.DueDate, request.EndAt, request.AllDay,
                request.Location?.Trim(), request.Status, request.Rrule?.Trim(), request.RemindBeforeMin, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return Result<TaskResponse>.Ok(ToResponse(task, await ResolveAsync(office, [task.AssigneeId], ct)));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var task = await repository.FindAsync(office, id, ct);
            if (task is null || !(await access.ScopeAsync(office, ct)).CanSee(task.MatterId, task.AssigneeId, task.CreatedBy))
            {
                return Result.NotFound("Ajanda kaydı bulunamadı.");
            }

            return await repository.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Ajanda kaydı bulunamadı.");
        }, ct);

    private Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(Guid office, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return list.Count == 0 ? Task.FromResult(NoNames) : users.DisplayNamesAsync(office, list, ct);
    }

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static TaskResponse ToResponse(WorkTask t, IReadOnlyDictionary<Guid, string> names) =>
        new(t.Id, t.Category, t.Title, t.Description, t.Type, t.Status, t.MatterId, t.AssigneeId,
            t.AssigneeId is { } a && names.TryGetValue(a, out var n) ? n : null, t.ClientId,
            t.DueAt, t.EndAt, t.AllDay, t.Location, t.Rrule, t.RemindBeforeMin, t.ReminderStatus,
            t.CompletedAt, t.CreatedAt);
}
