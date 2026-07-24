using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Law;

/// <summary>
/// Search for a keyword within one Sigorta Tahkim journal issue: downloads the issue PDF (by number
/// from a <c>search_decisions</c> source=sigorta_tahkim result), splits it into individual
/// arbitration decisions, and returns those mentioning the keyword, ranked by match count with a
/// short excerpt. Direct PDF download — no API key. Ported from Python
/// <c>search_within_sigorta_tahkim</c>.
/// </summary>
public sealed class SearchWithinSigortaTahkimTool(SigortaTahkimClient client) : IAgentTool
{
    private const int ExcerptRadius = 200;

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "issue_number": { "type": "string", "description": "Dergi sayı numarası (sigorta_tahkim arama sonucundan)" },
            "keyword": { "type": "string", "description": "Sayı içinde aranacak kelime/ifade (TR, büyük/küçük harf duyarsız)" },
            "max_results": { "type": "integer", "description": "En fazla eşleşen karar sayısı (varsayılan 10)" }
          },
          "required": ["issue_number", "keyword"]
        }
        """);

    public ToolSpec Spec => new("search_within_sigorta_tahkim",
        "Tek bir Sigorta Tahkim karar dergisi sayısı içinde anahtar kelime arar ve eşleşen hakem kararlarını döner.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var issueNumber = ToolArgs.String(arguments, "issue_number");
        var keyword = ToolArgs.String(arguments, "keyword");
        if (string.IsNullOrWhiteSpace(issueNumber) || string.IsNullOrWhiteSpace(keyword))
        {
            return ToolResult.Error("'issue_number' ve 'keyword' gereklidir.");
        }
        var maxResults = Math.Max(1, ToolArgs.Int(arguments, "max_results") ?? 10);

        var (text, _) = await client.DownloadIssueTextAsync(issueNumber, ct);
        if (string.IsNullOrEmpty(text))
        {
            return ToolResult.Error($"Sayı {issueNumber} indirilemedi.");
        }

        var decisions = SigortaTahkim.SplitIntoDecisions(text);
        var keywordLower = SigortaTahkim.TurkishLower(keyword);

        var matches = new List<Match>();
        foreach (var (header, body) in decisions)
        {
            var bodyLower = SigortaTahkim.TurkishLower(body);
            var count = CountOccurrences(bodyLower, keywordLower);
            if (count == 0)
            {
                continue;
            }
            var pos = bodyLower.IndexOf(keywordLower, StringComparison.Ordinal);
            var start = Math.Max(0, pos - ExcerptRadius);
            var end = Math.Min(body.Length, pos + keyword.Length + ExcerptRadius);
            var excerpt = body[start..end].Trim();
            if (start > 0)
            {
                excerpt = "..." + excerpt;
            }
            if (end < body.Length)
            {
                excerpt += "...";
            }
            matches.Add(new Match(header, count, excerpt, body.Length));
        }

        var ranked = matches.OrderByDescending(m => m.RelevanceScore).Take(maxResults).ToList();

        return ToolResult.Ok(ToolArgs.Result(new
        {
            issue_number = issueNumber,
            keyword,
            total_decisions = decisions.Count,
            matching_decisions = ranked.Count,
            results = ranked,
        }));
    }

    private sealed record Match(string Header, int RelevanceScore, string Excerpt, int BodyLength);

    private static int CountOccurrences(string haystack, string needle)
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
}
