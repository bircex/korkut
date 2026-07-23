using Korkut.Domain.Settings;

namespace Korkut.Application.Settings;

/// <summary>
/// Persistence port for drafting-style profiles. A profile is found by its layer — the
/// office one carries <see cref="Guid.Empty"/> as the user — so lookups go through
/// (office, scope, user) rather than an id.
/// </summary>
public interface IStyleProfileRepository
{
    Task<StyleProfile?> FindAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default);

    /// <summary>Tracked read, for mutation.</summary>
    Task<StyleProfile?> FindForUpdateAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default);

    /// <summary>Tracks a new profile; the caller commits it with <see cref="SaveChangesAsync"/>.</summary>
    Task AddAsync(StyleProfile profile, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
