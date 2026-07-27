using Korkut.Organization;
using System.Collections.Concurrent;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// Single-use, short-lived store of pending OIDC challenges, keyed by state.
/// <see cref="ConsumeAsync"/> atomically removes the entry, so a replayed callback
/// finds nothing. In-process for now; a Redis-backed store is a drop-in for
/// multi-instance deploys (KDM-018).
/// </summary>
public sealed class InMemoryOidcFlowStore : IOidcFlowStore
{
    private readonly ConcurrentDictionary<string, (OidcChallenge Challenge, DateTimeOffset ExpiresAt)> _pending = new();

    public Task SaveAsync(OidcChallenge challenge, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        _pending[challenge.State] = (challenge, expiresAt);
        return Task.CompletedTask;
    }

    public Task<OidcChallenge?> ConsumeAsync(string state, DateTimeOffset now, CancellationToken ct = default)
    {
        if (_pending.TryRemove(state, out var entry) && entry.ExpiresAt > now)
        {
            return Task.FromResult<OidcChallenge?>(entry.Challenge);
        }

        return Task.FromResult<OidcChallenge?>(null);
    }
}
