using Korkut.Organization;
using Korkut.Organization.Services;
using Korkut.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// Wires the runtime Google OIDC dependencies (KDM-016): the HTTP token exchanger and
/// the ID-token validator backed by Google's live JWKS (discovered + cached + rotated
/// by <see cref="ConfigurationManager{T}"/>), plus the sign-in orchestrator.
/// </summary>
public static class GoogleOidcRuntimeSetup
{
    private const string Discovery = "https://accounts.google.com/.well-known/openid-configuration";

    public static IServiceCollection AddGoogleOidcRuntime(this IServiceCollection services)
    {
        services.AddHttpClient<IOidcTokenExchanger, GoogleTokenExchanger>();

        // One ConfigurationManager per process caches Google's signing keys; the
        // validator pulls the current keys per validation.
        var config = new ConfigurationManager<OpenIdConnectConfiguration>(Discovery, new OpenIdConnectConfigurationRetriever());
        services.AddSingleton<IGoogleIdTokenValidator>(sp =>
        {
            var options = sp.GetRequiredService<GoogleOidcOptions>();
            return new GoogleIdTokenValidator(
                options.ClientId,
                () => config.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult().SigningKeys);
        });

        services.AddScoped<GoogleSignInService>();
        return services;
    }
}
