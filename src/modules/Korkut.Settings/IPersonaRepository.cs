namespace Korkut.Settings;

/// <summary>Persistence port for the office persona (one row per office, keyed by office).</summary>
public interface IPersonaRepository
{
    Task<Persona?> FindAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracked read, for mutation.</summary>
    Task<Persona?> FindForUpdateAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracks a new persona; the caller commits it with <see cref="SaveChangesAsync"/>.</summary>
    Task AddAsync(Persona persona, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
