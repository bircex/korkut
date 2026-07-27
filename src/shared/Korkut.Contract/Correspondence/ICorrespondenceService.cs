namespace Korkut.Contract.Correspondence;

/// <summary>Correspondence-register ("evrak") use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface ICorrespondenceService
{
    Task<Result<IReadOnlyList<CorrespondenceResponse>>> ListAsync(string? direction, CancellationToken ct = default);

    Task<Result<CorrespondenceResponse>> CreateAsync(CreateCorrespondenceRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
