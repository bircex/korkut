using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Korkut.Tools.Law;

/// <summary>
/// Text helpers shared by the legal-source clients: a dependency-free HTML→Markdown-ish
/// reduction (public portals serve HTML rulings) and fixed-size Markdown paging for documents
/// too large to return whole. Mirrors the Python <c>html_to_markdown</c> / <c>chunk_markdown</c>.
/// </summary>
public static partial class LegalText
{
    /// <summary>
    /// Reduce an HTML legal document to readable plain text: block tags become line breaks,
    /// inline tags are dropped, entities decoded, runs of blank lines collapsed. Good enough for
    /// an LLM to read a ruling; not a full Markdown renderer.
    /// </summary>
    public static string HtmlToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = ScriptStyle().Replace(html, " ");
        text = Break().Replace(text, "\n");
        text = BlockClose().Replace(text, "\n\n");
        text = ListItem().Replace(text, "\n- ");
        text = AnyTag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = BlankLines().Replace(text, "\n\n");
        text = TrailingSpaces().Replace(text, "\n");
        return text.Trim();
    }

    /// <summary>Extract text from a PDF (decision or journal) as plain text. Mirrors the Python
    /// <c>pdf_to_markdown</c> (pypdf) — page texts joined by blank lines.</summary>
    public static string PdfToText(byte[] data)
    {
        using var doc = PdfDocument.Open(data);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            var text = page.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                sb.Append(text).Append("\n\n");
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Return the inner HTML of the first <c>&lt;div class="… className …"&gt;</c>, balancing nested
    /// divs; falls back to the whole document when no matching div is found (so the caller can always
    /// hand the result to <see cref="HtmlToMarkdown"/>). Mirrors the Python <c>extract_div_html</c>.
    /// </summary>
    public static string ExtractDivHtml(string html, string className)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html ?? string.Empty;
        }
        foreach (Match open in DivOpen().Matches(html))
        {
            var classes = open.Groups["class"].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (Array.IndexOf(classes, className) < 0)
            {
                continue;
            }
            var start = open.Index + open.Length;
            var depth = 1;
            foreach (Match tag in DivAny().Matches(html, start))
            {
                depth += tag.Value.StartsWith("</", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
                if (depth == 0)
                {
                    return html[start..tag.Index].Trim();
                }
            }
            return html[start..].Trim();
        }
        return html;
    }

    /// <summary>
    /// Return one <paramref name="pageSize"/>-character slice of a long document plus pagination
    /// metadata (1-indexed, clamped): (Markdown, Page, TotalPages).
    /// </summary>
    public static (string Markdown, int Page, int TotalPages) ChunkMarkdown(string markdown, int page = 1, int pageSize = 5000)
    {
        var text = markdown ?? string.Empty;
        var totalPages = text.Length == 0 ? 1 : Math.Max(1, (int)Math.Ceiling(text.Length / (double)pageSize));
        page = Math.Max(1, Math.Min(page, totalPages));
        var start = (page - 1) * pageSize;
        var length = Math.Min(pageSize, text.Length - start);
        return (length > 0 ? text.Substring(start, length) : string.Empty, page, totalPages);
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyle();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex Break();

    [GeneratedRegex(@"</(p|div|h[1-6]|tr|table|ul|ol|section|article)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockClose();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex TrailingSpaces();

    [GeneratedRegex(@"<div\b[^>]*\bclass\s*=\s*[""'](?<class>[^""']*)[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DivOpen();

    [GeneratedRegex(@"</?div\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DivAny();
}
