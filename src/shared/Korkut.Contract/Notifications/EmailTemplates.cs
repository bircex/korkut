using System.Net;

namespace Korkut.Contract.Notifications;

/// <summary>
/// The single HTML shell every transactional e-mail uses (see docs/EMAILS.md for the trigger
/// inventory). Inline styles only (mail clients ignore stylesheets), one accent, one optional
/// call-to-action button. Callers pass PLAIN TEXT paragraphs — everything is HTML-encoded here,
/// so no caller can inject markup; links ride exclusively on the CTA.
/// </summary>
public static class EmailTemplates
{
    /// <summary>Render the standard shell: title + paragraphs + optional CTA button.</summary>
    public static string Render(
        string title, IReadOnlyList<string> paragraphs, (string Text, string Url)? cta = null)
    {
        var body = string.Join("", paragraphs.Select(p =>
            $"<p style=\"margin:0 0 12px;color:#3f3f46;font-size:14px;line-height:1.6;\">{WebUtility.HtmlEncode(p)}</p>"));

        var button = cta is { } c
            ? "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:20px 0 4px;\"><tr><td style=\"border-radius:10px;background:#18181b;\">"
              + $"<a href=\"{WebUtility.HtmlEncode(c.Url)}\" style=\"display:inline-block;padding:11px 22px;color:#ffffff;font-size:14px;font-weight:600;text-decoration:none;\">{WebUtility.HtmlEncode(c.Text)}</a>"
              + "</td></tr></table>"
              + $"<p style=\"margin:8px 0 0;color:#a1a1aa;font-size:12px;line-height:1.5;word-break:break-all;\">Düğme çalışmazsa bu adresi tarayıcınıza yapıştırın: {WebUtility.HtmlEncode(c.Url)}</p>"
            : string.Empty;

        return
            "<!doctype html><html lang=\"tr\"><body style=\"margin:0;padding:0;background:#f4f4f5;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f4f5;padding:32px 12px;\"><tr><td align=\"center\">"
            + "<table role=\"presentation\" width=\"560\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;width:100%;\">"
            + "<tr><td style=\"padding:0 4px 14px;\"><span style=\"font-size:18px;font-weight:800;color:#18181b;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;\">Korkut</span></td></tr>"
            + "<tr><td style=\"background:#ffffff;border:1px solid #e4e4e7;border-radius:14px;padding:28px;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;\">"
            + $"<h1 style=\"margin:0 0 14px;color:#18181b;font-size:18px;line-height:1.4;\">{WebUtility.HtmlEncode(title)}</h1>"
            + body + button
            + "</td></tr>"
            + "<tr><td style=\"padding:14px 4px 0;color:#a1a1aa;font-size:12px;font-family:-apple-system,'Segoe UI',Roboto,Arial,sans-serif;\">"
            + "Bu e-posta Korkut tarafından otomatik gönderildi; yanıtlanmaz. Bu işlemi siz başlatmadıysanız yok sayabilirsiniz."
            + "</td></tr></table></td></tr></table></body></html>";
    }
}
