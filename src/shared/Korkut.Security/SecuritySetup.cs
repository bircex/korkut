using Korkut.Core;
using Korkut.Organization;
using Korkut.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Security;

/// <summary>
/// DI registration for local authentication, recovery (KDM-015), two-factor sign-in and sessions.
/// Members sign in with e-mail (the installation admin: with its user name) and a password only.
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
        services.AddScoped<Korkut.Organization.IAccountLocator, Korkut.Security.Auth.AccountLocator>();
        services.AddScoped<Korkut.Organization.IResetTokenResolver, Korkut.Security.Auth.ResetTokenResolver>();
        services.AddScoped<Korkut.Organization.IPasswordResetTokenRepository, Korkut.Security.Auth.PasswordResetTokenRepository>();
        services.AddScoped<Korkut.Organization.ISecurityNotifier, Korkut.Security.Auth.EmailSecurityNotifier>();

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

            byte[] parsed;
            try
            {
                parsed = Convert.FromBase64String(key);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("KORKUT_TOTP_KEY is not valid base64 (need base64 of exactly 32 random bytes, e.g. `openssl rand -base64 32`).");
            }

            // Fail FAST at boot with a clear message — a wrong-size key otherwise surfaces as a
            // cryptic AesGcm error on the first 2FA enrollment (hex-encoded keys decode to 48 bytes).
            return parsed.Length == 32
                ? new AesGcmSecretEncryptor(parsed)
                : throw new InvalidOperationException(
                    $"KORKUT_TOTP_KEY decodes to {parsed.Length} bytes; AES-256-GCM needs exactly 32 (use `openssl rand -base64 32`).");
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
}
