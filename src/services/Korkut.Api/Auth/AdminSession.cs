using System.Security.Cryptography;
using System.Text;
using Korkut.Security;

namespace Korkut.Api.Auth;

/// <summary>
/// Cookie-based sessions for the platform-admin console (the admin twin of
/// <see cref="SessionManager"/>). All cookies are httpOnly and PATH-SCOPED to /admin/api so
/// they never ride along to office endpoints. Tokens are minted with the ADMIN audience
/// (<see cref="AdminAccessTokens"/>): the full session carries role "platform_admin"; the
/// short-lived pending cookie carries "admin_pending_totp" / "admin_pending_bootstrap" between
/// the Google callback and the mandatory TOTP step and grants NO API access (the auth handler
/// only accepts "platform_admin"). Admins are not tenant users, so the claims' user id is a
/// deterministic hash of the allowlisted e-mail and the e-mail itself rides in the
/// security-stamp slot.
/// </summary>
public sealed class AdminSession(AdminAccessTokens tokens, IConfiguration configuration)
{
    public const string AccessCookie = Korkut.Web.Auth.AdminAuthHandler.AccessCookie;
    public const string PendingCookie = "korkut_admin_2fa";
    public const string DeniedCookie = "korkut_admin_denied";
    public const string CookiePath = "/admin/api";

    public const string AdminRole = Korkut.Web.Auth.AdminAuthHandler.AdminRole;
    public const string PendingTotpRole = "admin_pending_totp";
    public const string PendingBootstrapRole = "admin_pending_bootstrap";

    private static readonly TimeSpan AccessMaxAge = TimeSpan.FromHours(12);
    private static readonly TimeSpan PendingMaxAge = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DeniedMaxAge = TimeSpan.FromMinutes(2);

    private bool Secure => configuration["KORKUT_SECURE_COOKIES"] == "1";

    /// <summary>Deterministic admin principal id — admins have no tenant user row.</summary>
    public static Guid IdFor(string normalizedEmail)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)).AsSpan(0, 16));

    public void Issue(HttpResponse response, string email)
    {
        var token = tokens.Mint(new AccessClaims(IdFor(email), Guid.Empty, AdminRole, email));
        Set(response, AccessCookie, token, AccessMaxAge);
        Delete(response, PendingCookie);
        Delete(response, DeniedCookie);
    }

    public void SetPending(HttpResponse response, string email, bool enrolled)
    {
        var role = enrolled ? PendingTotpRole : PendingBootstrapRole;
        Set(response, PendingCookie, tokens.Mint(new AccessClaims(IdFor(email), Guid.Empty, role, email)), PendingMaxAge);
        Delete(response, DeniedCookie);
    }

    public void SetDenied(HttpResponse response) => Set(response, DeniedCookie, "1", DeniedMaxAge);

    public void Clear(HttpResponse response)
    {
        Delete(response, AccessCookie);
        Delete(response, PendingCookie);
        Delete(response, DeniedCookie);
    }

    /// <summary>Validated full-session claims from the access cookie; null when absent/stale.</summary>
    public async Task<AccessClaims?> ReadAsync(HttpRequest request)
    {
        var raw = request.Cookies[AccessCookie];
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var claims = await tokens.ValidateAsync(raw);
        return claims is { Role: AdminRole } ? claims : null;
    }

    /// <summary>Validated pending-2FA claims (either stage); null when absent/stale.</summary>
    public async Task<AccessClaims?> ReadPendingAsync(HttpRequest request)
    {
        var raw = request.Cookies[PendingCookie];
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var claims = await tokens.ValidateAsync(raw);
        return claims is { Role: PendingTotpRole or PendingBootstrapRole } ? claims : null;
    }

    public bool IsDenied(HttpRequest request) => request.Cookies[DeniedCookie] == "1";

    private void Set(HttpResponse response, string name, string value, TimeSpan maxAge) =>
        response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = Secure,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
            MaxAge = maxAge,
        });

    private void Delete(HttpResponse response, string name) =>
        response.Cookies.Delete(name, new CookieOptions { Path = CookiePath, Secure = Secure, SameSite = SameSiteMode.Lax });
}
