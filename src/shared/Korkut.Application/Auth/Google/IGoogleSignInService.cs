namespace Korkut.Application.Auth.Google;

/// <summary>
/// Google OIDC sign-in: authorization code + PKCE + state + nonce, an exact pre-registered
/// redirect URI, deterministic account linking (by provider subject; e-mail only for the
/// very first link), and the 2FA policy gate. Completing the callback never issues a
/// session by itself — it reports what the caller is entitled to.
/// </summary>
public interface IGoogleSignInService
{
    /// <summary>Whether the deployment has Google credentials at all.</summary>
    bool IsConfigured { get; }

    /// <summary>Starts the flow: stores a single-use challenge and returns Google's consent URL.</summary>
    Task<string> BeginAsync(CancellationToken ct = default);

    /// <summary>Completes the callback. The state must match a stored, unused challenge.</summary>
    Task<GoogleSignInResult> CompleteAsync(string state, string code, CancellationToken ct = default);
}
