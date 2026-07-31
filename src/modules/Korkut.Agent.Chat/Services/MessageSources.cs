using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Contract.Agent.Chat;

namespace Korkut.Agent.Chat.Services;

/// <summary>
/// The per-message legal sources (the UI's "İlgili Kararlar" drawer data): normalization of the
/// raw tool-loop output and (de)serialization for chat_messages.sources_json. Stored in the same
/// snake_case shape the API speaks, so the stored JSON and the wire JSON are one format. Reads are
/// tolerant: a null/corrupt payload yields null rather than failing a session load.
/// </summary>
internal static class MessageSources
{
    // Bounds for the payload: duplicates collapse to the first hit (a decision can come back from
    // several searches), the list is hard-capped, and snippets are trimmed so sources_json stays
    // small. Order = the order the model saw the results.
    internal const int MaxPerMessage = 40;

    internal const int MaxSnippetChars = 300;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static List<SourceRef>? Normalize(IReadOnlyList<SourceRef>? raw)
    {
        if (raw is not { Count: > 0 })
        {
            return null;
        }

        var seen = new HashSet<(string, string)>();
        var result = new List<SourceRef>();
        foreach (var s in raw)
        {
            if (!seen.Add((s.Source.ToLowerInvariant(), s.Id)) || result.Count >= MaxPerMessage)
            {
                continue;
            }

            result.Add(s.Snippet is { Length: > MaxSnippetChars } snip
                ? s with { Snippet = snip[..MaxSnippetChars].TrimEnd() + "…" }
                : s);
        }

        return result.Count > 0 ? result : null;
    }

    public static string? Serialize(IReadOnlyList<SourceRef>? sources)
        => sources is { Count: > 0 } ? JsonSerializer.Serialize(sources, Options) : null;

    public static IReadOnlyList<SourceRef>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<SourceRef>>(json, Options);
            return parsed is { Count: > 0 } ? parsed : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
