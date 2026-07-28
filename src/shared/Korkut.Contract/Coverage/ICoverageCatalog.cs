using Korkut.Contract.Coverage.Responses;

namespace Korkut.Contract.Coverage;

/// <summary>Reads the coverage figures out of the on-disk legal catalog.</summary>
public interface ICoverageCatalog
{
    CoverageResponse Coverage();
}
