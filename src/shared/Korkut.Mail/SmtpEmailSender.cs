using Korkut.Contract.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Korkut.Mail;

/// <summary>Immutable SMTP settings resolved once from the environment at startup.</summary>
public sealed record SmtpSettings(
    string Host, int Port, string User, string Password, string FromAddress, string FromName);

/// <summary>
/// MailKit-backed SMTP sender for the single operator account. One short-lived connection per
/// message (STARTTLS on 587) — sufficient for low-volume transactional mail. Failures throw;
/// callers wrap sends so a mail error never breaks the primary operation.
/// </summary>
public sealed partial class SmtpEmailSender(SmtpSettings settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(settings.User, settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        LogSent(logger, to, subject);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent e-mail to {Recipient} (subject: {Subject}).")]
    private static partial void LogSent(ILogger logger, string recipient, string subject);
}
