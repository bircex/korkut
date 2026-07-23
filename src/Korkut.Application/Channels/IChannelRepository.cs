using Korkut.Domain.Channels;

namespace Korkut.Application.Channels;

/// <summary>
/// Persistence port for messaging channels. The aggregate is keyed by (office, kind) rather
/// than a surrogate id, so this port is written out instead of deriving from
/// <c>IRepository&lt;T&gt;</c>; the tenancy rule is the same — the office comes first on
/// every call.
/// </summary>
public interface IChannelRepository
{
    Task<Channel?> FindAsync(Guid officeId, string kind, CancellationToken ct = default);

    /// <summary>Tracked read, for mutation.</summary>
    Task<Channel?> FindForUpdateAsync(Guid officeId, string kind, CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> ListAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracks a new channel; the caller commits it with <see cref="SaveChangesAsync"/>.</summary>
    Task AddAsync(Channel channel, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
