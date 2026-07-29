namespace Korkut.Contract.Deadlines;

/// <summary>Deadline (süre) use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IDeadlineService
{
    Task<Result<IReadOnlyList<DeadlineResponse>>> ListAsync(Guid? matterId, CancellationToken ct = default);

    Task<Result<DeadlineResponse>> CreateAsync(CreateDeadlineRequest request, CancellationToken ct = default);

    /// <summary>Completes the deadline once; completing an already-completed one is NotFound.</summary>
    Task<Result> CompleteAsync(Guid id, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
