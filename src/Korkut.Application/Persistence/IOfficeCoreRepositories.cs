using Korkut.Domain.OfficeCore;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>Persistence port for matters + the court-title lookup used for the snapshot rule.</summary>
public interface IMatterRepository
{
    Task<Matter> AddAsync(Matter matter, CancellationToken ct = default);

    /// <summary>Active matters (soft-deleted excluded), newest first.</summary>
    Task<IReadOnlyList<Matter>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<Matter?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<bool> SoftDeleteAsync(Guid officeId, Guid id, DateTimeOffset deletedAt, CancellationToken ct = default);

    /// <summary>The current display title of a court in the office, or null if unknown.</summary>
    Task<string?> GetCourtNameAsync(Guid officeId, Guid courtId, CancellationToken ct = default);
}

/// <summary>Persistence port for matter hearings.</summary>
public interface IHearingRepository
{
    Task<Hearing> AddAsync(Hearing hearing, CancellationToken ct = default);

    Task<IReadOnlyList<Hearing>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default);
}
