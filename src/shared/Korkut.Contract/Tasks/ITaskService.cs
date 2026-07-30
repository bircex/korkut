namespace Korkut.Contract.Tasks;

/// <summary>Agenda-record use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface ITaskService
{
    Task<Result<IReadOnlyList<TaskResponse>>> ListAsync(
        Guid? matterId, string? status = null, string? category = null, CancellationToken ct = default);

    Task<Result<TaskResponse>> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);

    Task<Result<TaskResponse>> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
