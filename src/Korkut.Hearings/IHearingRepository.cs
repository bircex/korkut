using Korkut.Core;

namespace Korkut.Hearings;

/// <summary>Persistence port for matter hearings.</summary>
public interface IHearingRepository : IRepository<Hearing>
{
    /// <summary>A matter's hearings, earliest first.</summary>
    Task<IReadOnlyList<Hearing>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default);
}
