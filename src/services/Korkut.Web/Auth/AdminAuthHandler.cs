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

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var claims = await adminTokens.ValidateAsync(header["Bearer ".Length..]);
        if (claims is null)
        {
            return AuthenticateResult.Fail("Invalid or non-admin access token.");
        }

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, claims.UserId.ToString()));
        identity.AddClaim(new Claim(AdminClaim, "true"));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
