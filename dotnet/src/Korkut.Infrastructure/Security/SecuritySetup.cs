using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IUserDirectory, EfUserDirectory>();
        services.AddScoped<ISecurityNotifier, LoggingSecurityNotifier>();
        services.AddScoped<AuthService>();

        // Google OIDC: bind to the EXISTING exact env names (preserved from Python).
        services.AddSingleton(BindGoogleOptions(configuration));
        services.AddSingleton<IOidcFlowStore, InMemoryOidcFlowStore>();
        services.AddScoped<IGoogleUserDirectory, EfGoogleUserDirectory>();

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
