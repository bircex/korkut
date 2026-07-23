using Korkut.Application.Correspondence.Requests;
using Korkut.Application.Correspondence.Responses;

namespace Korkut.Application.Correspondence;

/// <summary>Correspondence-register ("evrak") use-cases.</summary>
public interface ICorrespondenceService
{
    Task<IReadOnlyList<CorrespondenceResponse>> ListAsync(string? direction, CancellationToken ct = default);

    Task<CorrespondenceResponse> CreateAsync(CreateCorrespondenceRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
