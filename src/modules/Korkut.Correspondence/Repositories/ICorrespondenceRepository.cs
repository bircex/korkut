using Korkut.Correspondence.Entities;
using Korkut.Core;

namespace Korkut.Correspondence;

/// <summary>Persistence port for the correspondence register.</summary>
internal interface ICorrespondenceRepository : IRepository<CorrespondenceItem>
{
    /// <summary>The register, optionally narrowed to one direction, newest first.</summary>
    Task<IReadOnlyList<CorrespondenceItem>> ListAsync(Guid officeId, string? direction, CancellationToken ct = default);
}
