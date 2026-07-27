using Korkut.Application.Coverage.Responses;

namespace Korkut.Application.Coverage;

/// <summary>
/// What legal ground the assistant covers. This is reference data derived from the static
/// catalog, so it is identical for every office and needs no tenant scope.
/// </summary>
public interface ICoverageService
{
    CoverageResponse Get();
}
