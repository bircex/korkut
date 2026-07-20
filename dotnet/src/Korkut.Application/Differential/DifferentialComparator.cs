using System.Text.Json.Nodes;

namespace Korkut.Application.Differential;

public enum MismatchSeverity
{
    /// <summary>Cosmetic (e.g. set-like reordering) — does not block cutover.</summary>
    Low,

    /// <summary>Contract-breaking (shape/type/status/value) — blocks cutover.</summary>
    High,
}

/// <summary>One difference between the Python and .NET normalized responses.</summary>
public sealed record Mismatch(string Path, string Kind, MismatchSeverity Severity, string? Left, string? Right);

/// <summary>
/// Compares two already-normalized responses (KDM-034). Because volatile values are
/// masked upstream, a surviving difference is a genuine contract difference. Shape,
/// type, status and value differences are HIGH severity (block cutover); a set-like
/// array that differs only in order is LOW.
/// </summary>
public static class DifferentialComparator
{
    public static IReadOnlyList<Mismatch> Compare(JsonNode? left, JsonNode? right)
    {
        var mismatches = new List<Mismatch>();
        Walk("$", left, right, mismatches);
        return mismatches;
    }

    /// <summary>True when nothing HIGH-severity remains — the cutover gate (acceptance 1).</summary>
    public static bool NoHighSeverity(IEnumerable<Mismatch> mismatches) => !mismatches.Any(m => m.Severity == MismatchSeverity.High);

    private static void Walk(string path, JsonNode? left, JsonNode? right, List<Mismatch> outp)
    {
        if (left is null && right is null)
        {
            return;
        }

        if (left is null || right is null)
        {
            outp.Add(new Mismatch(path, "presence", MismatchSeverity.High, left?.ToJsonString(), right?.ToJsonString()));
            return;
        }

        if (KindOf(left) != KindOf(right))
        {
            outp.Add(new Mismatch(path, "type", MismatchSeverity.High, KindOf(left), KindOf(right)));
            return;
        }

        switch (left)
        {
            case JsonObject lo when right is JsonObject ro:
                foreach (var key in lo.Select(p => p.Key).Union(ro.Select(p => p.Key)).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var hasL = lo.ContainsKey(key);
                    var hasR = ro.ContainsKey(key);
                    if (!hasL || !hasR)
                    {
                        outp.Add(new Mismatch($"{path}.{key}", "field", MismatchSeverity.High, hasL ? "present" : "absent", hasR ? "present" : "absent"));
                        continue;
                    }

                    Walk($"{path}.{key}", lo[key], ro[key], outp);
                }

                break;

            case JsonArray la when right is JsonArray ra:
                if (la.Count != ra.Count)
                {
                    outp.Add(new Mismatch(path, "length", MismatchSeverity.High, la.Count.ToString(), ra.Count.ToString()));
                    break;
                }

                if (SameMultisetDifferentOrder(la, ra))
                {
                    outp.Add(new Mismatch(path, "order", MismatchSeverity.Low, "reordered", "reordered"));
                    break;
                }

                for (var i = 0; i < la.Count; i++)
                {
                    Walk($"{path}[{i}]", la[i], ra[i], outp);
                }

                break;

            default:
                var ls = left.ToJsonString();
                var rs = right.ToJsonString();
                if (ls != rs)
                {
                    outp.Add(new Mismatch(path, "value", MismatchSeverity.High, ls, rs));
                }

                break;
        }
    }

    private static bool SameMultisetDifferentOrder(JsonArray a, JsonArray b)
    {
        var sa = a.Select(x => x?.ToJsonString() ?? "null").OrderBy(x => x, StringComparer.Ordinal);
        var sb = b.Select(x => x?.ToJsonString() ?? "null").OrderBy(x => x, StringComparer.Ordinal);
        var ordered = a.Select(x => x?.ToJsonString() ?? "null").SequenceEqual(b.Select(x => x?.ToJsonString() ?? "null"));
        return !ordered && sa.SequenceEqual(sb);
    }

    private static string KindOf(JsonNode node) => node switch
    {
        JsonObject => "object",
        JsonArray => "array",
        _ => "scalar",
    };
}
