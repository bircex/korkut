
namespace Korkut.Knowledge;

/// <summary>
/// A boundary-aware character-window chunker: fills windows up to <see cref="MaxChars"/>, but
/// cuts at the last paragraph / line / sentence / word break before the limit so a passage never
/// ends mid-word. Consecutive windows overlap by <see cref="Overlap"/> characters so context that
/// straddles a cut survives in both. Deterministic, dependency-free, and language-agnostic (works
/// for Turkish legal text). The size/overlap are part of the index contract — changing them
/// changes retrieval, so a change should bump the embedding version at the ingestion seam.
/// </summary>
public sealed class ChunkerService : IChunkerService
{
    public const int MaxChars = 1000;

    private const int Overlap = 200;

    private static readonly string[] Breaks = ["\n\n", "\n", ". ", " "];

    public IReadOnlyList<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var s = text.ReplaceLineEndings("\n").Trim();
        var chunks = new List<string>();
        var start = 0;
        while (start < s.Length)
        {
            var end = Math.Min(start + MaxChars, s.Length);
            if (end < s.Length)
            {
                var boundary = BestBreak(s, start, end);
                if (boundary > start)
                {
                    end = boundary;
                }
            }

            var piece = s[start..end].Trim();
            if (piece.Length > 0)
            {
                chunks.Add(piece);
            }

            if (end >= s.Length)
            {
                break;
            }

            // Step forward, leaving an overlap tail; the guard prevents a zero/negative step.
            start = Math.Max(end - Overlap, start + 1);
        }

        return chunks;
    }

    // The end of the last natural break (paragraph → line → sentence → word) within (start, end].
    private static int BestBreak(string s, int start, int end)
    {
        foreach (var sep in Breaks)
        {
            var idx = s.LastIndexOf(sep, end - 1, end - start, StringComparison.Ordinal);
            if (idx > start)
            {
                return idx + sep.Length;
            }
        }

        return end;
    }
}
