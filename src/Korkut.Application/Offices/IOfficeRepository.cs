using Korkut.Domain.Offices;

namespace Korkut.Application.Offices;

/// <summary>
/// Persistence port for offices. Unlike every other repository this one is NOT
/// office-scoped: offices are the tenant root, so there is no outer tenant to scope them
/// by. Its callers are platform-admin use-cases (cross-tenant by design) and the caller's
/// own office lookup.
/// </summary>
public interface IOfficeRepository
{
    Task<Office?> FindAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracked read, for mutation.</summary>
    Task<Office?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Every office, by name.</summary>
    Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
