using Korkut.Core;
using Korkut.Domain.Correspondence;

namespace Korkut.Application.Correspondence;

/// <summary>Persistence port for the correspondence register.</summary>
public interface ICorrespondenceRepository : IRepository<CorrespondenceItem>
{
    /// <summary>The register, optionally narrowed to one direction, newest first.</summary>
    Task<IReadOnlyList<CorrespondenceItem>> ListAsync(Guid officeId, string? direction, CancellationToken ct = default);
}
