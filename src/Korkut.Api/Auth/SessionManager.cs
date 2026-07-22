using Korkut.Infrastructure.Security;

namespace Korkut.Api.Auth;

/// <summary>
/// Establishes and clears cookie-based sessions (KDM-030 contract). Tokens ride in
/// httpOnly cookies — never readable by JS — so the SPA holds no token. The access
/// token cookie (<c>korkut_at</c>) is what <see cref="AccessTokenAuthHandler"/>
/// authenticates with; the refresh cookie (<c>korkut_rt</c>) is scoped to the refresh
/// path only; the pending cookie (<c>korkut_2fa</c>) carries a half-authenticated
/// user between the password and TOTP steps and grants NO API access.
/// </summary>
public sealed class SessionManager(AccessTokenService accessTokens, RefreshTokenService refreshTokens, IConfiguration configuration)
{
    private const string AccessCookie = Korkut.Web.Auth.AccessTokenAuthHandler.AccessCookie;
    public const string RefreshCookie = "korkut_rt";
    private const string PendingCookie = "korkut_2fa";
    private const string SignupCookie = "korkut_signup";
    private const string RefreshPath = "/api/auth";

    private static readonly TimeSpan AccessMaxAge = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshMaxAge = TimeSpan.FromDays(14);
    private static readonly TimeSpan PendingMaxAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SignupMaxAge = TimeSpan.FromMinutes(15);

    private bool Secure => configuration["KORKUT_SECURE_COOKIES"] == "1";

    /// <summary>Mint an access token + issue a refresh token and set both cookies. Runs in the caller's tenant scope.</summary>
    public async Task IssueAsync(HttpResponse response, Guid officeId, Guid userId, string role, Guid stamp, CancellationToken ct)
    {
        var access = accessTokens.Mint(new AccessClaims(userId, officeId, role, stamp.ToString()));
        var refresh = await refreshTokens.IssueAsync(officeId, userId, ct);
        SetCookie(response, AccessCookie, access, AccessMaxAge, "/");
        SetCookie(response, RefreshCookie, refresh.RawToken, RefreshMaxAge, RefreshPath);
        Delete(response, PendingCookie, "/");
    }

    /// <summary>Refresh the access cookie only (after a rotation) and rotate the refresh cookie.</summary>
    public void Rotate(HttpResponse response, Guid officeId, Guid userId, string role, Guid stamp, string newRefreshRaw)
    {
        var access = accessTokens.Mint(new AccessClaims(userId, officeId, role, stamp.ToString()));
        SetCookie(response, AccessCookie, access, AccessMaxAge, "/");
        SetCookie(response, RefreshCookie, newRefreshRaw, RefreshMaxAge, RefreshPath);
    }

    /// <summary>Stash a half-authenticated user (password OK, TOTP pending) in a short-lived cookie.</summary>
    public void SetPending(HttpResponse response, Guid officeId, Guid userId, string role, Guid stamp)
    {
        var token = accessTokens.Mint(new AccessClaims(userId, officeId, role, stamp.ToString()));
        SetCookie(response, PendingCookie, token, PendingMaxAge, "/api/auth");
    }

    /// <summary>Read + validate the pending-2FA cookie; null if absent/expired/invalid.</summary>
    public Task<AccessClaims?> ReadPendingAsync(HttpRequest request)
    {
        var raw = request.Cookies[PendingCookie];
        return string.IsNullOrEmpty(raw) ? Task.FromResult<AccessClaims?>(null) : accessTokens.ValidateAsync(raw);
    }

    /// <summary>Stash a verified-but-account-less Google identity for the self-serve signup step.</summary>
    public void SetSignup(HttpResponse response, string ticket) =>
        SetCookie(response, SignupCookie, ticket, SignupMaxAge, "/");

    public string? ReadSignup(HttpRequest request) => request.Cookies[SignupCookie];

    public void ClearSignup(HttpResponse response) => Delete(response, SignupCookie, "/");

    public void Clear(HttpResponse response)
    {
        Delete(response, AccessCookie, "/");
        Delete(response, RefreshCookie, RefreshPath);
        Delete(response, PendingCookie, "/api/auth");
    }

    private void SetCookie(HttpResponse response, string name, string value, TimeSpan maxAge, string path) =>
        response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = Secure,
            SameSite = SameSiteMode.Lax,
            Path = path,
            MaxAge = maxAge,
        });

    private void Delete(HttpResponse response, string name, string path) =>
        response.Cookies.Delete(name, new CookieOptions { Path = path, Secure = Secure, SameSite = SameSiteMode.Lax });
}
