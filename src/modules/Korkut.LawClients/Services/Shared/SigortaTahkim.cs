using System.Text.RegularExpressions;

namespace Korkut.LawClients.Services.Shared;

/// <summary>
/// Shared helpers for Sigorta Tahkim Komisyonu (Insurance Arbitration Commission) journals: mapping
/// an issue number to its PDF filename, splitting an issue's text into individual decisions, and
/// Turkish-aware lowercasing. Used by <see cref="SigortaTahkimClient"/> and
/// <see cref="SearchWithinSigortaTahkimTool"/>. Ported from Python <c>_base/sigorta_tahkim.py</c>.
/// </summary>
internal static partial class SigortaTahkim
{
    public const string PdfBaseUrl = "https://www.sigortatahkim.org/content/CmsFiles/";

    private const int MinBodyLength = 1000;

    /// <summary>Map a journal issue number to its PDF filename on the commission's CMS.</summary>
    public static string PdfFilename(int issueNumber) => issueNumber switch
    {
        4 => "karardergisisayi4.pdf",
        >= 57 and <= 61 => $"revizekd{issueNumber}.pdf",
        _ => $"karardrgs{issueNumber}.pdf",
    };

    /// <summary>Split an issue's text into (header, body) pairs, dropping TOC-only entries.</summary>
    public static IReadOnlyList<(string Header, string Body)> SplitIntoDecisions(string markdown)
    {
        var parts = DecisionHeader().Split(markdown);
        var decisions = new List<(string, string)>();
        // Split() with one capture group yields: [pre, header1, body1, header2, body2, ...].
        for (var i = 1; i < parts.Length - 1; i += 2)
        {
            var header = parts[i].Trim();
            var body = (i + 1 < parts.Length ? parts[i + 1] : string.Empty).Trim();
            if (body.Length >= MinBodyLength)
            {
                decisions.Add((header, body));
            }
        }
        return decisions;
    }

    /// <summary>Turkish-aware lowercase: İ→i, I→ı (invariant lower mishandles these).</summary>
    public static string TurkishLower(string text)
    {
        var mapped = text
            .Replace('İ', 'i').Replace('I', 'ı')
            .Replace('Ç', 'ç').Replace('Ğ', 'ğ')
            .Replace('Ö', 'ö').Replace('Ş', 'ş').Replace('Ü', 'ü');
        return mapped.ToLowerInvariant();
    }

    // Decision header, e.g. "01.02.2024 Tarih ve K-2024/123 Sayılı Hakem Kararı".
    [GeneratedRegex(@"(\d{2}\.\d{2}\.\d{4}\s+Tarih\s+ve\s+K-\d{4}/\d+\s+Sayılı\s+Hakem\s+Kararı)")]
    private static partial Regex DecisionHeader();
}
