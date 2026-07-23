using Korkut.Application.Deadlines.Requests;
using Korkut.Application.Deadlines.Responses;

namespace Korkut.Application.Deadlines;

/// <summary>Deadline (süre) use-cases. Missing records raise <c>NotFoundException</c>.</summary>
public interface IDeadlineService
{
    Task<IReadOnlyList<DeadlineResponse>> ListAsync(Guid? matterId, CancellationToken ct = default);

    Task<DeadlineResponse> CreateAsync(CreateDeadlineRequest request, CancellationToken ct = default);

    /// <summary>Completes the deadline once; completing an already-completed one is not found.</summary>
    Task CompleteAsync(Guid id, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
