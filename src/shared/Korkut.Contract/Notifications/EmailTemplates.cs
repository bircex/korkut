using System.Net;

namespace Korkut.Contract.Notifications;

/// <summary>
/// The single HTML shell every transactional e-mail uses (see docs/EMAILS.md for the trigger
/// inventory). Inline styles only (mail clients ignore stylesheets), one accent, one optional
/// call-to-action button, the Korkut AI wordmark + hosted PNG logo in the header. Callers pass
/// PLAIN TEXT paragraphs — everything is HTML-encoded here, so no caller can inject markup;
/// links ride exclusively on the CTA.
/// </summary>
public static class EmailTemplates
{
    /// <summary>Render the standard shell: title + paragraphs + optional CTA + optional logo.</summary>
    public static string Render(
        string title, IReadOnlyList<string> paragraphs, (string Text, string Url)? cta = null, string? logoUrl = null)
    {
        var body = string.Join("", paragraphs.Select(p =>
            $"<p style=\"margin:0 0 12px;color:#3f3f46;font-size:14px;line-height:1.65;\">{WebUtility.HtmlEncode(p)}</p>"));

        var button = cta is { } c
            ? "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:22px 0 4px;\"><tr><td style=\"border-radius:10px;background:#18181b;\">"
              + $"<a href=\"{WebUtility.HtmlEncode(c.Url)}\" style=\"display:inline-block;padding:12px 26px;color:#ffffff;font-size:14px;font-weight:600;text-decoration:none;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;\">{WebUtility.HtmlEncode(c.Text)}</a>"
              + "</td></tr></table>"
              + $"<p style=\"margin:10px 0 0;color:#a1a1aa;font-size:12px;line-height:1.5;word-break:break-all;\">Düğme çalışmazsa bu adresi tarayıcınıza yapıştırın:<br>{WebUtility.HtmlEncode(c.Url)}</p>"
            : string.Empty;

        var logo = logoUrl is { Length: > 0 }
            ? $"<img src=\"{WebUtility.HtmlEncode(logoUrl)}\" width=\"36\" height=\"36\" alt=\"\" style=\"display:inline-block;vertical-align:middle;border-radius:9px;border:1px solid #e4e4e7;margin-right:10px;\">"
            : string.Empty;

        return
            "<!doctype html><html lang=\"tr\"><body style=\"margin:0;padding:0;background:#f4f4f5;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f4f5;padding:36px 12px;\"><tr><td align=\"center\">"
            + "<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;width:100%;\">"
            + "<tr><td style=\"padding:0 4px 16px;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;\">"
            + logo
            + "<span style=\"font-size:19px;font-weight:800;color:#18181b;vertical-align:middle;letter-spacing:-0.2px;\">Korkut AI</span>"
            + "</td></tr>"
            + "<tr><td style=\"background:#ffffff;border:1px solid #e4e4e7;border-radius:16px;padding:32px;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;\">"
            + $"<h1 style=\"margin:0 0 16px;color:#18181b;font-size:19px;line-height:1.4;letter-spacing:-0.2px;\">{WebUtility.HtmlEncode(title)}</h1>"
            + body + button
            + "</td></tr>"
            + "<tr><td style=\"padding:16px 4px 0;color:#a1a1aa;font-size:12px;line-height:1.6;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;\">"
            + "© Korkut AI · Avukatlar için yapay zekâ asistanı<br>"
            + "Bu e-posta otomatik gönderildi; yanıtlanmaz. Bu işlemi siz başlatmadıysanız yok sayabilirsiniz."
            + "</td></tr></table></td></tr></table></body></html>";
    }
}
