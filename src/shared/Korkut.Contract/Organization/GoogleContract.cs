namespace Korkut.Contract.Organization;

/// <summary>How a Google callback ended.</summary>
public enum GoogleSignInStatus
{
    /// <summary>Identity verified and linked; no second factor needed, a session may be issued.</summary>
    Authenticated,

    /// <summary>Identity verified, but Korkut TOTP must be completed before a full session.</summary>
    TwoFactorRequired,

    /// <summary>Rejected — see <see cref="GoogleSignInResult.Reason"/>.</summary>
    Rejected,
}

/// <summary>
/// The outcome of a Google callback. <see cref="User"/> is present exactly when an identity resolved
/// to an account, and even then a session may only be issued once <see cref="Status"/> says the
/// second factor is not pending.
/// </summary>
public sealed record GoogleSignInResult(GoogleSignInStatus Status, SessionUser? User, string? Reason)
{
    public static GoogleSignInResult Reject(string reason) => new(GoogleSignInStatus.Rejected, null, reason);

    public static GoogleSignInResult SignedIn(SessionUser user, bool twoFactorRequired) =>
        new(twoFactorRequired ? GoogleSignInStatus.TwoFactorRequired : GoogleSignInStatus.Authenticated, user, null);
}

/// <summary>
/// Google OIDC sign-in (cross-module contract): authorization code + PKCE + state + nonce, exact
/// pre-registered redirect URI, deterministic account linking, and the 2FA policy gate. Completing
/// the callback never issues a session by itself — it reports what the caller is entitled to.
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
