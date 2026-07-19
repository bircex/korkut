using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// DI registration for local authentication and recovery (KDM-015). Endpoints and
/// session issuance are wired in KDM-018; this exposes the services they consume.
/// </summary>
public static class SecuritySetup
{
    public static IServiceCollection AddKorkutAuth(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddScoped<IUserDirectory, EfUserDirectory>();
        services.AddScoped<ISecurityNotifier, LoggingSecurityNotifier>();
        services.AddScoped<AuthService>();
        return services;
    }
}
