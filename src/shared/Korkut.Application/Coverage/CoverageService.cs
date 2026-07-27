using Korkut.Application.Coverage.Responses;

namespace Korkut.Application.Coverage;

/// <summary>Coverage over the catalog port. The catalog is the single source of this answer.</summary>
public sealed class CoverageService(ICoverageCatalog catalog) : ICoverageService
{
    public CoverageResponse Get() => catalog.Coverage();
}
