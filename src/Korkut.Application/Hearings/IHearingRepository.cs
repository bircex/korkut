using Korkut.Application.Common;
using Korkut.Domain.Hearings;

namespace Korkut.Application.Hearings;

/// <summary>Persistence port for matter hearings.</summary>
public interface IHearingRepository : IRepository<Hearing>
{
    /// <summary>A matter's hearings, earliest first.</summary>
    Task<IReadOnlyList<Hearing>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default);
}
