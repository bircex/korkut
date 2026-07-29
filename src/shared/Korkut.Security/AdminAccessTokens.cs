using Korkut.Core;

namespace Korkut.Security;

/// <summary>
/// Admin access tokens — same signer as office tokens but a SEPARATE audience
/// (<see cref="AccessTokenService.AdminAudience"/>), so an office token can never be
/// replayed against an admin endpoint and vice versa (KDM-020).
/// </summary>
public sealed class AdminAccessTokens
{
    private readonly AccessTokenService _inner;

    public AdminAccessTokens(string secret, IClock clock)
        => _inner = new AccessTokenService(secret, clock, AccessTokenService.AdminAudience);

    public string Mint(AccessClaims claims) => _inner.Mint(claims);

    public Task<AccessClaims?> ValidateAsync(string token) => _inner.ValidateAsync(token);
}
