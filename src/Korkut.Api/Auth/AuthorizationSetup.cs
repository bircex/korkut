using Korkut.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Korkut.Api.Auth;

/// <summary>
/// Wires the access-token authentication scheme and a deny-by-default authorization
/// policy per permission (policy name == permission string). Endpoints require a
/// permission via <c>.RequireAuthorization(Permissions.X)</c> — never an inline role
/// check (KDM-019).
/// </summary>
public static class AuthorizationSetup
{
    public static IServiceCollection AddKorkutAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<AuthorizationService>();

        services.AddAuthentication(AccessTokenAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthHandler>(AccessTokenAuthHandler.SchemeName, _ => { })
            .AddScheme<AuthenticationSchemeOptions, AdminAuthHandler>(AdminAuthHandler.SchemeName, _ => { });

        var authz = services.AddAuthorizationBuilder();
        foreach (var permission in Permissions.All)
        {
            // Office endpoints accept ONLY the office scheme (audience) — not admin tokens.
            authz.AddPolicy(permission, policy => policy
                .AddAuthenticationSchemes(AccessTokenAuthHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(AccessTokenAuthHandler.PermissionClaim, permission));
        }

        // The platform-admin policy: separate scheme (admin audience) + admin marker.
        authz.AddPolicy(AdminAuthHandler.SchemeName, policy => policy
            .AddAuthenticationSchemes(AdminAuthHandler.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(AdminAuthHandler.AdminClaim));

        return services;
    }
}
