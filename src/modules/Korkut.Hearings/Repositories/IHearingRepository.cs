using Korkut.Hearings.Entities;
using Korkut.Core;

namespace Korkut.Hearings.Repositories;

/// <summary>Persistence port for matter hearings.</summary>
internal interface IHearingRepository : IRepository<Hearing>
{
    /// <summary>A matter's hearings, earliest first.</summary>
    Task<IReadOnlyList<Hearing>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default);
}
