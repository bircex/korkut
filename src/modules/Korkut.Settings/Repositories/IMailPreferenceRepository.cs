using Korkut.Settings.Entities;

namespace Korkut.Settings.Repositories;

/// <summary>Persistence port for the office mail preference (one row per office, keyed by office).</summary>
public interface IMailPreferenceRepository
{
    Task<MailPreference?> FindAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracked read, for mutation.</summary>
    Task<MailPreference?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracks a new preference; the caller commits it with <see cref="SaveChangesAsync"/>.</summary>
    Task AddAsync(MailPreference preference, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
