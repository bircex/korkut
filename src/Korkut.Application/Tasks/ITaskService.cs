using Korkut.Application.Tasks.Requests;
using Korkut.Application.Tasks.Responses;

namespace Korkut.Application.Tasks;

/// <summary>Task use-cases. Missing records raise <c>NotFoundException</c>.</summary>
public interface ITaskService
{
    Task<IReadOnlyList<TaskResponse>> ListAsync(Guid? matterId, CancellationToken ct = default);

    Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);

    Task<TaskResponse> SetStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
