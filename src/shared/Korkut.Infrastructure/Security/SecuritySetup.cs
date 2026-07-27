using Korkut.Core;
using Korkut.Application.Auth.Google;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// DI registration for local authentication, recovery (KDM-015) and Google OIDC
/// (KDM-016). Callback endpoints and session issuance are wired in KDM-018.
/// </summary>
public static class SecuritySetup
{
    public static IServiceCollection AddKorkutAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        // Local sign-in, lockout and password recovery (clean-architecture Auth slice):
        // the cross-tenant account/reset-token resolvers, the reset-token store, and the
        // service the controller talks to.
        services.AddScoped<Korkut.Organization.IAccountLocator, Korkut.Infrastructure.Auth.AccountLocator>();
        services.AddScoped<Korkut.Organization.IResetTokenResolver, Korkut.Infrastructure.Auth.ResetTokenResolver>();
        services.AddScoped<Korkut.Organization.IPasswordResetTokenRepository, Korkut.Infrastructure.Auth.PasswordResetTokenRepository>();
        services.AddScoped<Korkut.Organization.ISecurityNotifier, Korkut.Infrastructure.Auth.LoggingSecurityNotifier>();
        services.AddScoped<Korkut.Application.Auth.IAuthService, Korkut.Application.Auth.AuthService>();

        // Google OIDC: bind to the EXISTING exact env names (preserved from Python).
        services.AddSingleton(BindGoogleOptions(configuration));
        services.AddSingleton<IOidcFlowStore, InMemoryOidcFlowStore>();
        services.AddScoped<Korkut.Application.Auth.Google.IGoogleAccountDirectory, Korkut.Infrastructure.Auth.GoogleAccountDirectory>();
        services.AddScoped<Korkut.Application.Auth.Google.IGoogleSignInService, Korkut.Application.Auth.Google.GoogleSignInService>();
        // Self-serve onboarding: provisioning a disabled office for a verified Google identity.
        services.AddScoped<Korkut.Contract.Offices.IOfficeProvisioner, Korkut.Infrastructure.Offices.OfficeProvisioner>();

        // TOTP 2FA (KDM-017). The envelope key is operator-supplied; if it is missing
        // and 2FA is used, resolution fails closed — never a silent random fallback.
        services.AddSingleton<ISecretEncryptor>(sp =>
        {
            var key = sp.GetRequiredService<IConfiguration>()["KORKUT_TOTP_KEY"];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    "KORKUT_TOTP_KEY (base64, 32 bytes) is required to encrypt TOTP secrets.");
            }

            return new AesGcmSecretEncryptor(Convert.FromBase64String(key));
        });
        services.AddScoped<TwoFactorService>();
        services.AddScoped<Korkut.Organization.ITwoFactorChallenge>(sp => sp.GetRequiredService<TwoFactorService>());

        // Sessions (KDM-018): short-lived access tokens + rotating refresh families.
        services.TryAddSingleton<Korkut.Core.IClock>(Korkut.Core.SystemClock.Instance);
        services.AddSingleton(sp => new AccessTokenService(
            sp.GetRequiredService<IConfiguration>()["KORKUT_SECRET"] ?? string.Empty,
            sp.GetRequiredService<Korkut.Core.IClock>()));
        services.AddScoped<RefreshTokenService>();
        return services;
    }

    /// <summary>Reads GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET / GOOGLE_REDIRECT_URI verbatim.</summary>
    public static GoogleOidcOptions BindGoogleOptions(IConfiguration configuration) => new()
    {
        ClientId = configuration["GOOGLE_CLIENT_ID"] ?? string.Empty,
        ClientSecret = configuration["GOOGLE_CLIENT_SECRET"] ?? string.Empty,
        RedirectUri = configuration["GOOGLE_REDIRECT_URI"] ?? string.Empty,
    };
}
