namespace Korkut.Contract.Notifications;

/// <summary>
/// Sends transactional e-mail from the single operator-controlled account (there is no
/// per-office e-mail integration). Implementations are system-level singletons; when SMTP
/// is not configured a no-op logging implementation stands in so callers never fail.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>
/// Public-facing base URL of the app (KORKUT_APP_URL), used to build absolute links in
/// e-mail (e.g. invite links). Empty when unset — links then fall back to relative paths.
/// </summary>
public sealed record MailLinks(string AppBaseUrl)
{
    /// <summary>Absolute link to accept an invite, or the relative path when the base URL is unset.</summary>
    public string InviteLink(string rawToken)
    {
        var path = $"/?invite={rawToken}";
        return string.IsNullOrWhiteSpace(AppBaseUrl) ? path : $"{AppBaseUrl.TrimEnd('/')}{path}";
    }
}
