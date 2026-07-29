using System.Text;
using System.Text.RegularExpressions;
using Korkut.Contract.Pii;

namespace Korkut.Pii;

/// <summary>
/// Regex-based Turkish PII redactor. Detects e-mail, Turkish IBAN, TCKN (with the official
/// checksum, so random 11-digit numbers aren't false positives) and Turkish phone numbers
/// (requiring a +90/0 prefix or grouping separators, so bare digit runs like case numbers aren't
/// swept up). Overlapping candidates are resolved by priority (IBAN &gt; e-mail &gt; TCKN &gt; phone)
/// so a phone sub-match never eats an IBAN/TCKN. Placeholders are stable ASCII tokens
/// (<c>[[PII_KIND_n]]</c>) a model can echo verbatim, so a reply restores cleanly. Stateless and
/// thread-safe; per-turn state lives in the scope it hands out.
/// </summary>
public sealed partial class PiiRedactorService : IPiiRedactorService
{
    public RedactionResult Redact(string text)
    {
        var scope = new Scope();
        var masked = scope.Mask(text);
        return new RedactionResult(masked, scope.Matches.ToList());
    }

    public IPiiRedactionScope NewScope() => new Scope();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    // TR IBAN: TR + 2 check digits + 22 digits (26 chars total), optional single spaces between groups.
    [GeneratedRegex(@"(?<![A-Za-z0-9])TR\d{2}(?:\s?\d{4}){5}\s?\d{2}(?![A-Za-z0-9])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IbanPattern();

    // A standalone 11-digit run — validated by the TCKN checksum before it is accepted.
    [GeneratedRegex(@"(?<!\d)[1-9]\d{10}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex TcknCandidate();

    // Phone with a +90/0 prefix (separators optional; area code may be parenthesized).
    [GeneratedRegex(@"(?<!\d)(?:\+90|0)[\s.\-]?\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{2}[\s.\-]?\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePrefixed();

    // Phone with no prefix but explicit grouping separators (so bare digit runs don't match).
    [GeneratedRegex(@"(?<!\d)\(?\d{3}\)?[\s.\-]\d{3}[\s.\-]\d{2}[\s.\-]\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneSeparated();

    // Lower number = higher priority when two candidates overlap.
    private static int Priority(PiiKind kind) => kind switch
    {
        PiiKind.Iban => 0,
        PiiKind.Email => 1,
        PiiKind.Tckn => 2,
        _ => 3,
    };

    private static bool IsValidTckn(string s)
    {
        if (s.Length != 11 || s[0] == '0')
        {
            return false;
        }

        Span<int> d = stackalloc int[11];
        
        for (var i = 0; i < 11; i++)
        {
            if (!char.IsAsciiDigit(s[i]))
            {
                return false;
            }

            d[i] = s[i] - '0';
        }

        var oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        var evenSum = d[1] + d[3] + d[5] + d[7];
        var tenth = (((oddSum * 7) - evenSum) % 10 + 10) % 10;
        
        if (tenth != d[9])
        {
            return false;
        }

        var total = 0;
        
        for (var i = 0; i < 10; i++)
        {
            total += d[i];
        }

        return total % 10 == d[10];
    }

    /// <summary>A per-unit-of-work redaction scope with a shared value→placeholder map.</summary>
    private sealed class Scope : IPiiRedactionScope
    {
        private readonly Dictionary<string, PiiMatch> _byValue = new(StringComparer.Ordinal);
        
        private readonly Dictionary<PiiKind, int> _counters = new();

        public IReadOnlyCollection<PiiMatch> Matches => _byValue.Values;

        public string Mask(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var spans = Detect(text);
            
            if (spans.Count == 0)
            {
                return text;
            }

            var sb = new StringBuilder(text.Length);
            var cursor = 0;
            
            foreach (var (start, length, kind) in spans)
            {
                sb.Append(text, cursor, start - cursor);
                sb.Append(PlaceholderFor(kind, text.Substring(start, length)));
                cursor = start + length;
            }

            sb.Append(text, cursor, text.Length - cursor);
            return sb.ToString();
        }

        public string Restore(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var sb = new StringBuilder(text);
            foreach (var match in _byValue.Values)
            {
                sb.Replace(match.Placeholder, match.Value);
            }

            return sb.ToString();
        }

        private string PlaceholderFor(PiiKind kind, string value)
        {
            if (_byValue.TryGetValue(value, out var existing))
            {
                return existing.Placeholder;
            }

            var n = _counters.GetValueOrDefault(kind) + 1;
            _counters[kind] = n;
            var placeholder = $"[[PII_{kind.ToString().ToUpperInvariant()}_{n}]]";
            _byValue[value] = new PiiMatch(kind, value, placeholder);
            return placeholder;
        }
    }

    // Collect non-overlapping spans, highest-priority-wins on overlap, ordered left-to-right.
    private static List<(int Start, int Length, PiiKind Kind)> Detect(string text)
    {
        var candidates = new List<(int Start, int Length, PiiKind Kind)>();
        foreach (Match m in EmailPattern().Matches(text))
        {
            candidates.Add((m.Index, m.Length, PiiKind.Email));
        }

        foreach (Match m in IbanPattern().Matches(text))
        {
            candidates.Add((m.Index, m.Length, PiiKind.Iban));
        }

        foreach (Match m in TcknCandidate().Matches(text))
        {
            if (IsValidTckn(m.Value))
            {
                candidates.Add((m.Index, m.Length, PiiKind.Tckn));
            }
        }

        foreach (Match m in PhonePrefixed().Matches(text))
        {
            candidates.Add((m.Index, m.Length, PiiKind.Phone));
        }

        foreach (Match m in PhoneSeparated().Matches(text))
        {
            candidates.Add((m.Index, m.Length, PiiKind.Phone));
        }

        var selected = new List<(int Start, int Length, PiiKind Kind)>();
        foreach (var candidate in candidates.OrderBy(c => Priority(c.Kind)).ThenBy(c => c.Start).ThenByDescending(c => c.Length))
        {
            var end = candidate.Start + candidate.Length;
            var overlaps = selected.Any(s => candidate.Start < s.Start + s.Length && s.Start < end);
            if (!overlaps)
            {
                selected.Add(candidate);
            }
        }

        selected.Sort((a, b) => a.Start.CompareTo(b.Start));
        return selected;
    }
}
