using Korkut.Organization.Entities;

namespace Korkut.Organization.Repositories;

/// <summary>Persistence port for the office's roles; runs in the caller's tenant scope.</summary>
public interface IOfficeRoleRepository
{
    Task<IReadOnlyList<OfficeRole>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<OfficeRole?> FindAsync(Guid officeId, string key, CancellationToken ct = default);

    /// <summary>Tracked, for mutation.</summary>
    Task<OfficeRole?> FindForUpdateAsync(Guid officeId, string key, CancellationToken ct = default);

    void Add(OfficeRole role);

    void Remove(OfficeRole role);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
