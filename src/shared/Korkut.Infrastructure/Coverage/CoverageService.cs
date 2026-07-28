using Korkut.Contract.Coverage;
using Korkut.Contract.Coverage.Responses;

namespace Korkut.Infrastructure.Coverage;

/// <summary>Coverage over the catalog port. The catalog is the single source of this answer.</summary>
public sealed class CoverageService(ICoverageCatalog catalog) : ICoverageService
{
    public CoverageResponse Get() => catalog.Coverage();
}
