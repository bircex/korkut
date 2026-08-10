using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;

namespace Korkut.LawClients.Services.Sources;

/// <summary>One hit from the Tavily web-search API: the discovered page's title, url and snippet.</summary>
internal sealed record TavilyHit(string Title, string Url, string Content);

/// <summary>
/// Minimal client for the Tavily Search API (api.tavily.com). A few Turkish authorities (KVKK,
/// BDDK, Sigorta Tahkim) publish decisions on plain pages / PDF journals with no searchable
/// backend; we discover them by querying Tavily restricted to the authority's domain. One key
/// (<c>TAVILY_API_KEY</c>) covers the whole toolset. Ported from Python <c>_base/search_providers.py</c>.
/// </summary>
internal sealed class TavilyClient(IHttpClientFactory httpClientFactory, IEnvironment env)
{
    public const string KeyEnv = "TAVILY_API_KEY";

    public async Task<IReadOnlyList<TavilyHit>> SearchAsync(
        string query, IReadOnlyList<string> includeDomains, int maxResults, CancellationToken ct = default)
    {
        var key = env.Get(KeyEnv);
        
        if (string.IsNullOrWhiteSpace(key))
        {
            return [];
        }

        var payload = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["country"] = "turkey",
            ["max_results"] = maxResults,
            ["search_depth"] = "advanced",
        };
        
        if (includeDomains.Count > 0)
        {
            payload["include_domains"] = includeDomains;
        }

        var client = httpClientFactory.CreateClient("tavily");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, "/search")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var resp = await client.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var body = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!body.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
            return [];

        var hits = new List<TavilyHit>(results.GetArrayLength());
        
        foreach (var item in results.EnumerateArray())
        {
            hits.Add(new TavilyHit(
                Str(item, "title") ?? string.Empty,
                Str(item, "url") ?? string.Empty,
                Str(item, "content") ?? string.Empty));
        }
        
        return hits;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;
}
