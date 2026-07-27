using Korkut.Contract.Hearings;

namespace Korkut.Contract.Matters;

/// <summary>
/// Matter (dosya) use-cases (cross-module contract). Owns the court-title snapshot rule: linking a
/// court captures its current title onto the matter. Calls RETURN a <see cref="Result"/>.
/// </summary>
public interface IMatterService
{
    Task<Result<IReadOnlyList<MatterResponse>>> ListAsync(CancellationToken ct = default);

    /// <summary>The matter with its hearings; NotFound if missing/soft-deleted.</summary>
    Task<Result<MatterDetailResponse>> GetAsync(Guid id, CancellationToken ct = default);

    Task<Result<MatterResponse>> CreateAsync(CreateMatterRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
