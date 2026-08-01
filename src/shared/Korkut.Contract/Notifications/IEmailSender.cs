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
    public string InviteLink(string rawToken) => Absolute($"/?invite={rawToken}");

    /// <summary>Absolute link to the password-reset screen carrying the one-time token.</summary>
    public string ResetLink(string rawToken) => Absolute($"/giris?reset={rawToken}");

    /// <summary>Absolute link to the sign-in screen (post-approval welcome mail).</summary>
    public string LoginLink() => Absolute("/giris");

    /// <summary>E-posta başlığındaki logo (uygulama origin'inde barındırılan PNG; SVG mail
    /// istemcilerinde çalışmaz). Base URL yoksa null — şablon metin markasına düşer.</summary>
    public string? LogoUrl() => string.IsNullOrWhiteSpace(AppBaseUrl) ? null : Absolute("/korkut-logo-email.png");

    private string Absolute(string path)
        => string.IsNullOrWhiteSpace(AppBaseUrl) ? path : $"{AppBaseUrl.TrimEnd('/')}{path}";
}
