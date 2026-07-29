namespace Korkut.Contract.Tasks;

/// <summary>Task use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface ITaskService
{
    Task<Result<IReadOnlyList<TaskResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default);

    Task<Result<TaskResponse>> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);

    Task<Result<TaskResponse>> SetStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
