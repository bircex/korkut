using Korkut.Application.Coverage.Responses;

namespace Korkut.Application.Coverage;

/// <summary>Reads the coverage figures out of the on-disk legal catalog.</summary>
public interface ICoverageCatalog
{
    CoverageResponse Coverage();
}
