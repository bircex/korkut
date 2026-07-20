using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Korkut.Application.Differential;

/// <summary>
/// Canonicalizes an API response for cross-runtime differential comparison (KDM-034),
/// mirroring the Python <c>tests/compat/normalize.py</c> (KDM-003) EXACTLY so a .NET
/// response and a Python response reduce to the same comparable form.
///
/// Volatile details that are not part of the contract — generated ids, timestamps,
/// opaque tokens/cookies — are masked to a TYPE TOKEN (<c>&lt;uuid&gt;</c>,
/// <c>&lt;iso8601&gt;</c>, <c>&lt;int&gt;</c> …), never dropped: a shape/type change
/// still surfaces as a diff, while a value change (a different id) does not. This also
/// guarantees no real id/token/PII value can leak into a differential artifact.
/// </summary>
public static partial class ResponseNormalizer
{
    [GeneratedRegex(@"^(id|uid|uuid|.*_id|token|.*_token|created_at|updated_at|.*_at|iat|exp|session|cookie|nonce|state|csrf|etag)$", RegexOptions.IgnoreCase)]
    private static partial Regex VolatileKeyRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}")]
    private static partial Regex Iso8601Regex();

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-", RegexOptions.IgnoreCase)]
    private static partial Regex UuidRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")]
    private static partial Regex JwtRegex();

    private static readonly HashSet<string> VolatileHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "server", "content-length", "set-cookie", "x-request-id", "x-process-time",
    };

    /// <summary>Recursively canonicalize a JSON body. Keys are sorted; volatile keys/scalars masked.</summary>
    public static JsonNode? NormalizeBody(JsonNode? value, string? key = null)
    {
        switch (value)
        {
            case JsonObject obj:
            {
                var outp = new JsonObject();
                foreach (var name in obj.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var v = obj[name];
                    outp[name] = VolatileKeyRegex().IsMatch(name) ? TypeToken(v) : NormalizeBody(v, name);
                }

                return outp;
            }

            case JsonArray arr:
            {
                var outp = new JsonArray();
                foreach (var el in arr)
                {
                    outp.Add(NormalizeBody(el));
                }

                return outp;
            }

            default:
                return MaskScalar(value);
        }
    }

    /// <summary>Canonical record for one request: status + selected headers + normalized body.</summary>
    public static JsonObject NormalizeResult(int status, IReadOnlyDictionary<string, string> headers, JsonNode? body, IEnumerable<string>? captureHeaders = null)
    {
        var normalizedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, val) in headers)
        {
            normalizedHeaders[name.ToLowerInvariant()] = VolatileHeaders.Contains(name) ? "<present>" : val;
        }

        var kept = new JsonObject();
        foreach (var h in (captureHeaders ?? []).OrderBy(h => h, StringComparer.Ordinal))
        {
            kept[h] = normalizedHeaders.TryGetValue(h.ToLowerInvariant(), out var v) ? v : "<absent>";
        }

        return new JsonObject
        {
            ["status"] = status,
            ["headers"] = kept,
            ["body"] = NormalizeBody(body),
        };
    }

    private static JsonNode? MaskScalar(JsonNode? value)
    {
        if (value is JsonValue jv && jv.TryGetValue<string>(out var s))
        {
            if (Iso8601Regex().IsMatch(s))
            {
                return "<iso8601>";
            }

            if (UuidRegex().IsMatch(s))
            {
                return "<uuid>";
            }

            if (JwtRegex().IsMatch(s))
            {
                return "<jwt>";
            }
        }

        return value?.DeepClone();
    }

    private static JsonNode TypeToken(JsonNode? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        if (value is JsonObject)
        {
            return "<dict>";
        }

        if (value is JsonArray)
        {
            return "<list>";
        }

        if (value is JsonValue v)
        {
            var el = v.GetValue<JsonElement>();
            return el.ValueKind switch
            {
                JsonValueKind.True or JsonValueKind.False => "<bool>",
                JsonValueKind.String => "<str>",
                JsonValueKind.Number => el.TryGetInt64(out _) ? "<int>" : "<float>",
                JsonValueKind.Null => "<null>",
                _ => "<other>",
            };
        }

        return "<other>";
    }
}
