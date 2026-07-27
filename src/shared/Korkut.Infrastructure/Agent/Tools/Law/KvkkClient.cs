using Korkut.Application.Agent.Tools;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Korkut.Infrastructure.Agent.Tools.Law;

/// <summary>
/// KVKK (Personal Data Protection Authority) board decision summaries. kvkk.gov.tr has no
/// searchable API, so search discovers decision pages via Tavily restricted to the domain; fetch
/// pulls the page and extracts the <c>blog-post-inner</c> body. Requires <c>TAVILY_API_KEY</c> for
/// search. Ported from Python <c>search_kvkk</c> + <c>fetch_kvkk</c>.
/// </summary>
public sealed partial class KvkkClient(TavilyClient tavily, IHttpClientFactory httpClientFactory) : ISourceClient
{
    private static readonly string[] Domains = ["kvkk.gov.tr"];

    public string Source => "kvkk";
    public string? RequiresKey => TavilyClient.KeyEnv;
    public bool SupportsFetch => true;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 20);
        var hits = await tavily.SearchAsync($"{query} \"karar özeti\"".Trim(), Domains, limit, ct);

        var results = new List<SourceResult>(hits.Count);
        
        foreach (var hit in hits)
        {
            var date = Date().Match(hit.Title);
            var number = Number().Match(hit.Title);
            results.Add(new SourceResult(
                Source: "kvkk",
                Id: DecisionId(hit.Url) ?? hit.Url,
                Title: hit.Title,
                Date: date.Success ? date.Groups[1].Value : null,
                DecisionNumber: number.Success ? number.Groups[1].Value : null,
                Url: hit.Url,
                Snippet: Truncate(hit.Content, 500)));
        }
        
        return results;
    }

    public async Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
    {
        var url = id.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? id
            : $"https://www.kvkk.gov.tr/Icerik/{id}";

        var client = httpClientFactory.CreateClient("legal");
        using var resp = await client.GetAsync(url, ct);
        
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }
        
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var html = Encoding.UTF8.GetString(bytes);

        var bodyHtml = LegalText.ExtractDivHtml(html, "blog-post-inner");
        var markdown = LegalText.HtmlToMarkdown(bodyHtml);
        
        var (chunk, page, totalPages) = LegalText.ChunkMarkdown(markdown,
            ToolArgs.Int(args, "page") ?? 1, ToolArgs.Int(args, "page_size") ?? 5000);
        
        return $"Kaynak: {url}\nSayfa: {page}/{totalPages}\n\n{chunk}";
    }

    private static string? DecisionId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }
        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 && parts[0].Equals("icerik", StringComparison.OrdinalIgnoreCase)
            ? string.Join('/', parts[1..])
            : null;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"(\d{1,2}/\d{1,2}/\d{4})")]
    private static partial Regex Date();

    [GeneratedRegex(@"(\d{4}/\d+)")]
    private static partial Regex Number();
}
