using Korkut.Contract.Coverage.Responses;

namespace Korkut.Contract.Coverage;

/// <summary>
/// What legal ground the assistant covers. This is reference data derived from the static
/// catalog, so it is identical for every office and needs no tenant scope.
/// </summary>
public interface ICoverageService
{
    CoverageResponse Get();
}
