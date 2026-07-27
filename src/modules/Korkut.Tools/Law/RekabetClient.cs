using Korkut.Tools.Tools;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Korkut.Tools.Law;

/// <summary>
/// Rekabet Kurumu (Competition Authority) decisions. No JSON API and no key: the results page is
/// server-rendered HTML, so search fetches it and parses the decision list. Each decision links to
/// a landing page identified by a <c>kararId</c> GUID; <c>decision_type</c> maps to the portal's
/// <c>KararTuruID</c>. Search-only. Ported from Python <c>search_rekabet</c>.
/// </summary>
public sealed partial class RekabetClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string BaseUrl = "https://www.rekabet.gov.tr";
    private const string DecisionPath = "/Karar";
    private const int ResultsPerPage = 10;

    // Friendly decision-type names → the portal's KararTuruID GUIDs ("" = all types).
    private static readonly Dictionary<string, string> TypeGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = "",
        ["all"] = "",
        ["tumu"] = "",
        ["birlesme_devralma"] = "2fff0979-9f9d-42d7-8c2e-a30705889542",
        ["diger"] = "dda8feaf-c919-405c-9da1-823f22b45ad9",
        ["menfi_tespit_muafiyet"] = "95ccd210-5304-49c5-b9e0-8ee53c50d4e8",
        ["ozellestirme"] = "e1f14505-842b-4af5-95d1-312d6de1a541",
        ["rekabet_ihlali"] = "720614bf-efd1-4dca-9785-b98eb65f2677",
    };

    public string Source => "rekabet";
    public string? RequiresKey => null;
    public bool SupportsFetch => false;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var decisionType = (ToolArgs.String(args, "decision_type") ?? string.Empty).Trim();
        var guid = TypeGuids.GetValueOrDefault(decisionType, string.Empty);
        var page = Math.Max(1, ToolArgs.Int(args, "page") ?? 1);
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, ResultsPerPage);

        var url = $"{BaseUrl}/tr/Kararlar?sayfaAdi={Uri.EscapeDataString(query)}&YayinlanmaTarihi=&PdfText=" +
                  $"&KararTuruID={Uri.EscapeDataString(guid)}&KararSayisi=&KararTarihi=";
        if (page > 1)
        {
            url += $"&page={page}";
        }

        var client = httpClientFactory.CreateClient("legal");
        using var resp = await client.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(ct);

        var results = new List<SourceResult>();
        
        foreach (Match table in DecisionTable().Matches(html))
        {
            var rows = Row().Matches(table.Value).Select(r =>
                Cell().Matches(r.Value).Select(c => Clean(c.Groups[1].Value)).ToList()).ToList();

            var link = Anchor().Matches(table.Value)
                .Select(a => (Href: WebUtility.HtmlDecode(a.Groups[1].Value), Text: Clean(a.Groups[2].Value)))
                .FirstOrDefault(a => a.Href.Contains("kararId=", StringComparison.OrdinalIgnoreCase));
            var kararId = KararId(link.Href);
            
            if (string.IsNullOrEmpty(kararId))
            {
                continue;
            }

            var decisionNo = rows.Count > 0 && rows[0].Count > 1 ? rows[0][1] : string.Empty;
            var decisionDate = rows.Count > 1 && rows[1].Count > 0 ? rows[1][0] : string.Empty;
            var typeLabel = rows.Count > 1 && rows[1].Count > 1 ? rows[1][1] : string.Empty;
            var pubDate = rows.Count > 0 && rows[0].Count > 0 ? rows[0][0] : string.Empty;

            results.Add(new SourceResult(
                Source: "rekabet",
                Id: kararId,
                Title: string.IsNullOrEmpty(link.Text) ? decisionNo : link.Text,
                Date: string.IsNullOrEmpty(decisionDate) ? pubDate : decisionDate,
                DecisionNumber: string.IsNullOrEmpty(decisionNo) ? null : decisionNo,
                Court: string.IsNullOrEmpty(typeLabel) ? null : typeLabel,
                Url: $"{BaseUrl}{DecisionPath}?kararId={kararId}"));

            if (results.Count >= limit)
            {
                break;
            }
        }
        return results;
    }

    public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    private static string KararId(string href)
    {
        if (string.IsNullOrEmpty(href))
        {
            return string.Empty;
        }
        
        var match = KararIdParam().Match(href);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string Clean(string html) =>
        WebUtility.HtmlDecode(Whitespace().Replace(Tag().Replace(html, " "), " ")).Trim();

    [GeneratedRegex(@"<table[^>]*class=""[^""]*equalDivide[^""]*""[^>]*>.*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DecisionTable();

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Row();

    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Cell();

    [GeneratedRegex(@"<a[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Anchor();

    [GeneratedRegex(@"kararId=([^&""]+)", RegexOptions.IgnoreCase)]
    private static partial Regex KararIdParam();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
