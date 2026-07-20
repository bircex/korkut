using System.Security.Cryptography;

namespace Korkut.Application.Security;

/// <summary>
/// Google OIDC options, bound to the existing exact env names
/// (GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET / GOOGLE_REDIRECT_URI) in KDM-033.
/// </summary>
public sealed class GoogleOidcOptions
{
    public const string GoogleIssuer = "https://accounts.google.com";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);
}

/// <summary>The per-login secrets: CSRF state, replay-guard nonce, and PKCE pair.</summary>
public sealed record OidcChallenge(string State, string Nonce, string CodeVerifier, string CodeChallenge)
{
    public static OidcChallenge Create()
    {
        var state = Rand();
        var nonce = Rand();
        var verifier = Rand();
        return new OidcChallenge(state, nonce, verifier, Pkce.S256(verifier));
    }

    private static string Rand() => Base64Url(RandomNumberGenerator.GetBytes(32));

    internal static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>PKCE S256 code-challenge derivation (RFC 7636).</summary>
public static class Pkce
{
    public static string S256(string codeVerifier)
    {
        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        return OidcChallenge.Base64Url(hash);
    }
}

/// <summary>Verified claims extracted from a Google ID token.</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string Nonce);

/// <summary>Validates a Google ID token's signature and standard claims, or throws.</summary>
public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default);
}

/// <summary>Exchanges an auth code (+ PKCE verifier) for tokens at Google's token endpoint.</summary>
public interface IOidcTokenExchanger
{
    Task<string> ExchangeForIdTokenAsync(string code, string codeVerifier, CancellationToken ct = default);
}

/// <summary>Server-side, single-use, short-lived store of pending OIDC challenges (keyed by state).</summary>
public interface IOidcFlowStore
{
    Task SaveAsync(OidcChallenge challenge, DateTimeOffset expiresAt, CancellationToken ct = default);

    /// <summary>Atomically fetch-and-remove by state; null if unknown/expired/already used (replay).</summary>
    Task<OidcChallenge?> ConsumeAsync(string state, DateTimeOffset now, CancellationToken ct = default);
}

/// <summary>Google-linked account lookup/linking over the user directory.</summary>
public interface IGoogleUserDirectory
{
    Task<AuthUser?> FindByGoogleSubAsync(string googleSub, CancellationToken ct);

    Task<IReadOnlyList<AuthUser>> FindByEmailAcrossOfficesAsync(string normalizedEmail, CancellationToken ct);

    Task LinkGoogleSubAsync(Guid userId, string googleSub, CancellationToken ct);

    Task<bool> IsOfficeDisabledAsync(Guid officeId, CancellationToken ct);

    Task<bool> OfficeRequiresTwoFactorAsync(Guid officeId, CancellationToken ct);

    Task<bool> UserHasTwoFactorAsync(Guid userId, CancellationToken ct);
}

public enum GoogleSignInStatus
{
    /// <summary>Identity verified and linked; no 2FA needed — a full session may be minted (KDM-018).</summary>
    Authenticated,

    /// <summary>Identity verified but Korkut TOTP must be completed before a full session (KDM-017).</summary>
    TwoFactorRequired,

    /// <summary>Rejected — see <see cref="GoogleSignInResult.Reason"/>.</summary>
    Rejected,
}

public sealed record GoogleSignInResult(GoogleSignInStatus Status, Guid? UserId, Guid? OfficeId, string? Reason)
{
    public static GoogleSignInResult Reject(string reason) => new(GoogleSignInStatus.Rejected, null, null, reason);
}
