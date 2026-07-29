using Korkut.Organization.Entities;
namespace Korkut.Organization;

/// <summary>Server-side, single-use, short-lived store of pending OIDC challenges (keyed by state).</summary>
public interface IOidcFlowStore
{
    Task SaveAsync(OidcChallenge challenge, DateTimeOffset expiresAt, CancellationToken ct = default);

    /// <summary>Atomically fetch-and-remove by state; null if unknown, expired or already used (replay).</summary>
    Task<OidcChallenge?> ConsumeAsync(string state, DateTimeOffset now, CancellationToken ct = default);
}
