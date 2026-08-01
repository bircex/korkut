using Korkut.Contract.Notifications;
using Korkut.Organization;
using Microsoft.Extensions.Logging;

namespace Korkut.Security.Auth;

/// <summary>
/// Sends a "your password was changed" e-mail via the system SMTP account. It records that a
/// password changed (never what to) and never carries a secret or token. A send failure is
/// logged and swallowed so a mail outage can never break the password-reset flow.
/// </summary>
public sealed partial class EmailSecurityNotifier(
    IEmailSender emailSender, ILogger<EmailSecurityNotifier> logger) : ISecurityNotifier
{
    public async Task NotifyPasswordChangedAsync(Guid officeId, string email, CancellationToken ct = default)
    {
        LogPasswordChanged(logger, officeId);

        const string subject = "Korkut hesabınızın parolası değişti";
        var body = EmailTemplates.Render(
            "Parolanız değiştirildi",
            [
                "Merhaba,",
                "Korkut hesabınızın parolası az önce değiştirildi. Bu işlemi siz yaptıysanız bir şey yapmanıza gerek yok.",
                "Bu işlemi siz yapmadıysanız hemen parola sıfırlama isteyin ve büro yöneticinizle iletişime geçin.",
            ]);

        try
        {
            await emailSender.SendAsync(email, subject, body, ct);
        }
        catch (Exception ex)
        {
            LogSendFailed(logger, officeId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password changed for a user in office {OfficeId}")]
    private static partial void LogPasswordChanged(ILogger logger, Guid officeId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send password-changed e-mail for office {OfficeId}")]
    private static partial void LogSendFailed(ILogger logger, Guid officeId, Exception ex);
}
