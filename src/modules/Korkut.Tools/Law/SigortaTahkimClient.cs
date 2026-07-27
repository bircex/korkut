using System.Text.Json;
using Korkut.Application.Agent.Tools;
using System.Text.RegularExpressions;

namespace Korkut.Tools.Law;

/// <summary>
/// Sigorta Tahkim Komisyonu (Insurance Arbitration Commission) decisions. The commission publishes
/// quarterly PDF journals with no searchable API: search discovers issues via Tavily; fetch
/// downloads an issue PDF (by number) and converts it. Requires <c>TAVILY_API_KEY</c> for search;
/// the fetch is a direct PDF download. Ported from Python <c>search_sigorta_tahkim</c> +
/// <c>fetch_sigorta_tahkim</c> + <c>_base/sigorta_tahkim.py</c>.
/// </summary>
public sealed partial class SigortaTahkimClient(TavilyClient tavily, IHttpClientFactory httpClientFactory) : ISourceClient
{
    private static readonly string[] Domains = ["sigortatahkim.org"];

    public string Source => "sigorta_tahkim";
    public string? RequiresKey => TavilyClient.KeyEnv;
    public bool SupportsFetch => true;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 20);
        var hits = await tavily.SearchAsync(query.Trim(), Domains, limit, ct);

        var results = new List<SourceResult>(hits.Count);
        foreach (var hit in hits)
        {
            results.Add(new SourceResult(
                Source: "sigorta_tahkim",
                Id: IssueNumber(hit.Url) ?? hit.Url,
                Title: hit.Title.Trim(),
                Url: hit.Url,
                Snippet: Truncate(hit.Content, 500)));
        }
        return results;
    }

    public async Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
    {
        var (text, pdfUrl) = await DownloadIssueTextAsync(id, ct);
        if (text is null)
        {
            return null;
        }
        var (chunk, page, totalPages) = LegalText.ChunkMarkdown(text,
            ToolArgs.Int(args, "page") ?? 1, ToolArgs.Int(args, "page_size") ?? 5000);
        return $"Kaynak: {pdfUrl}\nSayfa: {page}/{totalPages}\n\n{chunk}";
    }

    /// <summary>Download an issue PDF by number and return its extracted text + the PDF url.</summary>
    public async Task<(string? Text, string PdfUrl)> DownloadIssueTextAsync(string issueNumber, CancellationToken ct = default)
    {
        if (!int.TryParse(issueNumber, out var number))
        {
            return (null, string.Empty);
        }
        var pdfUrl = SigortaTahkim.PdfBaseUrl + SigortaTahkim.PdfFilename(number);
        var client = httpClientFactory.CreateClient("legal-insecure");
        using var resp = await client.GetAsync(pdfUrl, ct);
        if (!resp.IsSuccessStatusCode)
        {
            return (null, pdfUrl);
        }
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return (LegalText.PdfToText(bytes), pdfUrl);
    }

    private static string? IssueNumber(string url)
    {
        var match = Issue().Match(url);
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

    [GeneratedRegex(@"karardrgs(\d+)\.pdf|revizekd(\d+)\.pdf|karardergisisayi(\d+)\.pdf|say[ıi]\s*[-:]?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Issue();
}
