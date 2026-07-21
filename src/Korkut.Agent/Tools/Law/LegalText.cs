using System.Net;
using System.Text.RegularExpressions;

namespace Korkut.Agent.Tools.Law;

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
}
