using Korkut.Application.Coverage;
using Korkut.Application.Coverage.Responses;
using Korkut.Contract.Templates;

namespace Korkut.Infrastructure.Coverage;

/// <summary>
/// Reads coverage from the static legal catalog on disk. The same numbers for every
/// office, since the catalog is shared reference data.
/// </summary>
public sealed class CatalogCoverageReader(TemplateCatalog catalog) : ICoverageCatalog
{
    public CoverageResponse Coverage()
    {
        var coverage = catalog.Coverage();
        return new CoverageResponse(
            coverage.TotalTypes,
            coverage.ByCourt.Select(c => new CoverageCourtResponse(c.Label, c.Total)).ToList(),
            coverage.Sources,
            coverage.Legislation);
    }
}
