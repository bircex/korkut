using Korkut.Contract.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Mail;

/// <summary>
/// Registers the system-level <see cref="IEmailSender"/> as a singleton. When the SMTP
/// credentials (KORKUT_SMTP_USER/PASSWORD/FROM) are present it binds the MailKit sender;
/// otherwise it falls back to a log-only no-op so dev/test never fails for lack of config.
/// </summary>
public static class MailSetup
{
    public static IServiceCollection AddKorkutMail(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["KORKUT_SMTP_HOST"];
        var port = int.TryParse(configuration["KORKUT_SMTP_PORT"], out var p) ? p : 587;
        var user = configuration["KORKUT_SMTP_USER"];
        var password = configuration["KORKUT_SMTP_PASSWORD"];
        var from = configuration["KORKUT_SMTP_FROM"];
        var fromName = configuration["KORKUT_SMTP_FROM_NAME"];

        // Public app base URL for absolute links in e-mail (invite links). Empty is fine.
        services.TryAddSingleton(new MailLinks(configuration["KORKUT_APP_URL"] ?? string.Empty));

        if (string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(from))
        {
            services.TryAddSingleton<IEmailSender, NullEmailSender>();
            return services;
        }

        var settings = new SmtpSettings(
            Host: string.IsNullOrWhiteSpace(host) ? "smtp.gmail.com" : host!,
            Port: port,
            User: user!,
            Password: password!,
            FromAddress: from!,
            FromName: string.IsNullOrWhiteSpace(fromName) ? "Korkut" : fromName!);

        services.TryAddSingleton(settings);
        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
