using Korkut.Contract.Notifications;
using Microsoft.Extensions.Logging;

namespace Korkut.Mail;

/// <summary>
/// Stand-in used when SMTP is not configured (e.g. local dev). It logs what WOULD have been
/// sent instead of sending, so flows that trigger mail never fail for lack of configuration.
/// </summary>
public sealed partial class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        LogNotSent(logger, to, subject);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SMTP is not configured — e-mail to {Recipient} (subject: {Subject}) was NOT sent.")]
    private static partial void LogNotSent(ILogger logger, string recipient, string subject);
}
