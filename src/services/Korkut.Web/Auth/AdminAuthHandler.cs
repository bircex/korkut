using System.Security.Claims;
using System.Text.Encodings.Web;
using Korkut.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Korkut.Web.Auth;

/// <summary>
/// Separate authentication boundary for the platform-admin API: validates an
/// admin-audience access token and builds a principal with the admin marker claim.
/// Office tokens (office audience) fail validation here, so they can never reach an
/// admin endpoint (KDM-020).
/// </summary>
public sealed class AdminAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AdminAccessTokens adminTokens) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "korkut-admin";
    public const string AdminClaim = "korkut:admin";

    /// <summary>The admin console's httpOnly session cookie (set by the Api host's AdminSession).</summary>
    public const string AccessCookie = "korkut_admin_at";

    /// <summary>Role carried by a FULL admin session; the pending-2FA roles are rejected here.</summary>
    public const string AdminRole = "platform_admin";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Bearer (service/CI callers) first; the SPA's cookie session second.
        var header = Request.Headers.Authorization.ToString();
        var raw = header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? header["Bearer ".Length..]
            : Request.Cookies[AccessCookie];
        if (string.IsNullOrEmpty(raw))
        {
            return AuthenticateResult.NoResult();
        }

        var claims = await adminTokens.ValidateAsync(raw);
        if (claims is null)
        {
            return AuthenticateResult.Fail("Invalid or non-admin access token.");
        }

        // A pending-2FA token (admin audience but not the full role) grants no API access.
        if (Request.Cookies[AccessCookie] == raw && claims.Role != AdminRole)
        {
            return AuthenticateResult.Fail("Admin session incomplete.");
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, claims.UserId.ToString()));
        identity.AddClaim(new Claim(AdminClaim, "true"));
        if (claims.Role == AdminRole)
        {
            // Cookie sessions carry the allowlisted e-mail in the stamp slot (admins have no user row).
            identity.AddClaim(new Claim(ClaimTypes.Email, claims.SecurityStamp));
        }

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
