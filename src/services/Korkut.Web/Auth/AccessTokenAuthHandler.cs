using System.Security.Claims;
using System.Text.Encodings.Web;
using Korkut.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Korkut.Web.Auth;

/// <summary>
/// Turns a Bearer access token into a ClaimsPrincipal. Minimal token claims
/// (sub/oid/role/stamp) are expanded into per-permission claims from the office's role
/// (<see cref="Korkut.Core.IRolePermissions"/>, cached briefly), so policies check permissions,
/// never role names, and a role the office edits applies on the next request.
/// </summary>
public sealed class AccessTokenAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AccessTokenService accessTokens,
    Korkut.Core.IRolePermissions rolePermissions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "korkut-access";
    public const string AccessCookie = "korkut_at";
    public const string PermissionClaim = "perm";
    public const string OfficeClaim = "oid";
    public const string StampClaim = "stmp";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Accept the access token from the httpOnly session cookie (the SPA, KDM-030)
        // OR a Bearer header (API clients). The cookie is never readable by JS.
        var header = Request.Headers.Authorization.ToString();
        string? token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..] : null;
        token ??= Request.Cookies[AccessCookie];
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var claims = await accessTokens.ValidateAsync(token);
        if (claims is null)
        {
            return AuthenticateResult.Fail("Invalid or expired access token.");
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, claims.UserId.ToString()));
        identity.AddClaim(new Claim(OfficeClaim, claims.OfficeId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, claims.Role));
        identity.AddClaim(new Claim(StampClaim, claims.SecurityStamp));
        foreach (var permission in await rolePermissions.ForRoleAsync(claims.OfficeId, claims.Role, Context.RequestAborted))
        {
            identity.AddClaim(new Claim(PermissionClaim, permission));
        }

        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
