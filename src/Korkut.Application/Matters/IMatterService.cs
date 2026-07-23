using Korkut.Application.Matters.Requests;
using Korkut.Application.Matters.Responses;

namespace Korkut.Application.Matters;

/// <summary>
/// Matter (dosya) use-cases. Owns the court-title snapshot rule: linking a court captures its
/// current title onto the matter, so a later rename never rewrites the historical text.
/// </summary>
public interface IMatterService
{
    Task<IReadOnlyList<MatterResponse>> ListAsync(CancellationToken ct = default);

    /// <summary>The matter with its hearings; raises <c>NotFoundException</c> if missing/soft-deleted.</summary>
    Task<MatterDetailResponse> GetAsync(Guid id, CancellationToken ct = default);

    Task<MatterResponse> CreateAsync(CreateMatterRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
