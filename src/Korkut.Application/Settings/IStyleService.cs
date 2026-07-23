using Korkut.Application.Settings.Requests;
using Korkut.Application.Settings.Responses;

namespace Korkut.Application.Settings;

/// <summary>
/// Drafting-style use-cases. The office layer is shared; the user layer is always the
/// caller's own, so no request can point a write at somebody else's style.
/// </summary>
public interface IStyleService
{
    /// <summary>Both layers plus the effective overlay.</summary>
    Task<StyleResponse> GetAsync(CancellationToken ct = default);

    Task<StyleFieldsResponse> SaveUserAsync(SaveStyleRequest request, CancellationToken ct = default);

    Task<StyleFieldsResponse> SaveOfficeAsync(SaveStyleRequest request, CancellationToken ct = default);
}
