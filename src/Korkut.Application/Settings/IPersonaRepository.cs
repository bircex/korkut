using Korkut.Domain.Settings;

namespace Korkut.Application.Settings;

/// <summary>
/// Persistence port for the office persona. The office is the identity — at most one
/// persona per office — so this port is written out rather than derived from
/// <c>IRepository&lt;T&gt;</c>; the office still comes first on every call.
/// </summary>
public interface IPersonaRepository
{
    Task<Persona?> FindAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracked read, for mutation.</summary>
    Task<Persona?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracks a new persona; the caller commits it with <see cref="SaveChangesAsync"/>.</summary>
    Task AddAsync(Persona persona, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
