using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Korkut.LawClients.Services.Shared;

namespace Korkut.LawClients.Services.Sources;

/// <summary>
/// BDDK (Banking Regulation and Supervision Authority) decisions. bddk.org.tr has no searchable
/// API, so search discovers documents via Tavily restricted to the BDDK document domain; fetch
/// tries the known document-URL shapes (PDF or HTML) and converts. Requires <c>TAVILY_API_KEY</c>
/// for search. Ported from Python <c>search_bddk</c> + <c>fetch_bddk</c>.
/// </summary>
internal sealed partial class BddkClient(TavilyClient tavily, IHttpClientFactory httpClientFactory) : ISourceClient
{
    private static readonly string[] Domains = ["https://www.bddk.org.tr/Mevzuat/DokumanGetir"];

    private static readonly string[] UrlTemplates =
    [
        "https://www.bddk.org.tr/Mevzuat/DokumanGetir/{0}",
        "https://www.bddk.org.tr/Mevzuat/Liste/{0}",
        "https://www.bddk.org.tr/KurumHakkinda/EkGetir/13?ekId={0}",
        "https://www.bddk.org.tr/KurumHakkinda/EkGetir/5?ekId={0}",
    ];

    public string Source => "bddk";
    public string? RequiresKey => TavilyClient.KeyEnv;
    public bool SupportsFetch => true;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 20);
        var hits = await tavily.SearchAsync($"{query} \"Karar Sayısı\"".Trim(), Domains, limit, ct);

        var results = new List<SourceResult>();
        
        foreach (var hit in hits)
        {
            var docId = DocumentId(hit.Url);
            
            if (docId is null)
            {
                continue;
            }
            
            results.Add(new SourceResult(
                Source: "bddk",
                Id: docId,
                Title: hit.Title.Replace("[PDF] ", string.Empty).Trim(),
                Url: hit.Url,
                Snippet: Truncate(hit.Content, 500)));
        }
        
        return results;
    }

    public async Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("legal-insecure");
        
        foreach (var template in UrlTemplates)
        {
            var url = string.Format(template, id);
            
            try
            {
                using var resp = await client.GetAsync(url, ct);
                
                if (!resp.IsSuccessStatusCode)
                {
                    continue;
                }
                
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
                var markdown = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                    ? LegalText.PdfToText(bytes)
                    : LegalText.HtmlToMarkdown(Encoding.UTF8.GetString(bytes));
                
                var (chunk, page, totalPages) = LegalText.ChunkMarkdown(markdown,
                    ToolArgs.Int(args, "page") ?? 1, ToolArgs.Int(args, "page_size") ?? 5000);
                
                return $"Kaynak: {url}\nSayfa: {page}/{totalPages}\n\n{chunk}";
            }
            catch (HttpRequestException)
            {
                // Try the next URL shape.
            }
        }
        return null;
    }

    private static string? DocumentId(string url)
    {
        var match = DocId().Match(url);
        if (!match.Success)
        {
            return null;
        }
        for (var i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
            {
                return match.Groups[i].Value;
            }
        }
        return null;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"/DokumanGetir/(\d+)|/Liste/(\d+)|ekId=(\d+)")]
    private static partial Regex DocId();
}
