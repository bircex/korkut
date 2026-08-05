using System.Text.RegularExpressions;

namespace Korkut.LawClients.Services.Shared;

/// <summary>
/// Article-level search over a piece of legislation: split the full text into MADDE blocks and
/// evaluate a boolean keyword query (AND/OR/NOT uppercase, "tam ifade" in quotes) per article with
/// a match-count relevance score. Ported from the Python <c>article_search.py</c>
/// (<c>split_plain_text_into_articles</c> + <c>_matches_query</c>), with the markdown and
/// plain-text header patterns unified.
/// </summary>
internal static partial class MaddeSearch
{
    /// <summary>One article of a legislation text.</summary>
    public sealed record Madde(string No, string Content);

    /// <summary>
    /// Split legislation text into articles on MADDE headers. Handles the observed header shapes:
    /// <c>MADDE 1 –</c>, <c>Madde 328.</c>, <c>MADDE 5/A –</c>, soft-hyphen dashes, optional
    /// markdown bold, and EK / GEÇİCİ / MÜKERRER prefixes. Empty when no headers are found.
    /// </summary>
    public static IReadOnlyList<Madde> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var matches = Header().Matches(text);
        var articles = new List<Madde>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var prefix = m.Groups[1].Value;
            var number = m.Groups[2].Value;
            var no = prefix.Length == 0 ? number : $"{NormalizePrefix(prefix)} {number}";

            var start = m.Index;
            var end = i < matches.Count - 1 ? matches[i + 1].Index : text.Length;
            articles.Add(new Madde(no, text[start..end].Trim()));
        }
        return articles;
    }

    /// <summary>
    /// Evaluate a boolean query against one article. Quoted phrases are all required and score
    /// double; bare terms combine left-to-right with uppercase AND/OR/NOT (default AND). The score
    /// is the total occurrence count. Case-insensitive matching folds Turkish İ/I correctly.
    /// </summary>
    public static (bool IsMatch, int Score) Matches(string content, string query, bool caseSensitive = false)
    {
        var phrases = QuotedPhrase().Matches(query).Select(m => m.Groups[1].Value).ToList();
        var rest = phrases.Aggregate(query, (q, p) => q.Replace($"\"{p}\"", string.Empty));

        var haystack = caseSensitive ? content : SigortaTahkim.TurkishLower(content);

        bool? result = null;
        var score = 0;

        foreach (var phrase in phrases)
        {
            var needle = caseSensitive ? phrase : SigortaTahkim.TurkishLower(phrase);
            var count = Count(haystack, needle);
            if (count > 0)
            {
                score += count * 2; // exact phrases are worth more
                result ??= true;
            }
            else
            {
                return (false, 0); // every quoted phrase is required
            }
        }

        var tokens = OperatorSplit().Split(rest)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        var op = "AND";
        foreach (var token in tokens)
        {
            if (token is "AND" or "OR" or "NOT")
            {
                op = token;
                continue;
            }

            var needle = caseSensitive ? token : SigortaTahkim.TurkishLower(token);
            var count = Count(haystack, needle);
            var found = count > 0;

            switch (op)
            {
                case "AND":
                    // A first-term miss only seeds result=false (a later OR can still rescue the
                    // article); once a chain is underway a missing AND term eliminates it outright.
                    if (result is null)
                    {
                        result = found;
                    }
                    else if (!found)
                    {
                        return (false, 0);
                    }
                    score += count;
                    break;
                case "OR":
                    result = (result ?? false) || found;
                    score += count;
                    break;
                case "NOT":
                    if (found)
                    {
                        return (false, 0);
                    }
                    break;
            }
        }

        return result is true ? (true, score) : (false, 0);
    }

    /// <summary>
    /// A short context window around the first hit of the query's leading term (first quoted
    /// phrase, else first non-operator word); falls back to the article's opening 200 characters.
    /// </summary>
    public static string Preview(string content, string query, bool caseSensitive = false)
    {
        var term = QuotedPhrase().Matches(query).Select(m => m.Groups[1].Value).FirstOrDefault()
            ?? OperatorSplit().Split(query)
                .Select(t => t.Trim())
                .FirstOrDefault(t => t.Length > 0 && t is not ("AND" or "OR" or "NOT"));

        if (!string.IsNullOrEmpty(term))
        {
            var haystack = caseSensitive ? content : SigortaTahkim.TurkishLower(content);
            var needle = caseSensitive ? term : SigortaTahkim.TurkishLower(term);
            var pos = haystack.IndexOf(needle, StringComparison.Ordinal);
            if (pos >= 0)
            {
                var start = Math.Max(0, pos - 100);
                var end = Math.Min(content.Length, pos + needle.Length + 100);
                var preview = content[start..end];
                if (start > 0)
                {
                    preview = "..." + preview;
                }
                if (end < content.Length)
                {
                    preview += "...";
                }
                return preview;
            }
        }

        return content.Length <= 200 ? content : content[..200] + "...";
    }

    private static string NormalizePrefix(string prefix) => SigortaTahkim.TurkishLower(prefix) switch
    {
        "ek" => "Ek",
        "geçici" => "Geçici",
        _ => "Mükerrer",
    };

    private static int Count(string haystack, string needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    // Article header at line start: optional markdown bold, optional EK/GEÇİCİ/MÜKERRER prefix,
    // MADDE + number (with optional /A suffix), optional dot or soft hyphen, optional dash.
    [GeneratedRegex(@"(?:^|\n)[ \t]*\*{0,2}(?:(EK|Ek|GEÇİCİ|Geçici|MÜKERRER|Mükerrer)[ \t]+)?(?:MADDE|Madde)[ \t]+(\d+(?:/[A-Z])?)[ \t]*[.­]?[ \t]*[-–—]?")]
    private static partial Regex Header();

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex QuotedPhrase();

    [GeneratedRegex(@"\s+(AND|OR|NOT)\s+")]
    private static partial Regex OperatorSplit();
}
