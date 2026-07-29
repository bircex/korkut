using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Uyuşmazlık Mahkemesi (Court of Jurisdictional Disputes) decisions. The portal is a classic
/// ASP.NET WebForms search page: a GET seeds the <c>__VIEWSTATE</c> tokens + session cookie, a POST
/// replays them with the query to render a GridView; each row links straight to a decision PDF.
/// The host serves a legacy TLS stack (uses the insecure client). Ported from Python
/// <c>_base/uyusmazlik.py</c> + <c>search_uyusmazlik</c> + <c>fetch_uyusmazlik</c>.
/// </summary>
public sealed partial class UyusmazlikClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string BaseUrl = "https://kararlar.uyusmazlik.gov.tr";
    
    private static readonly string[] StateFields = ["__VIEWSTATE", "__VIEWSTATEGENERATOR", "__EVENTVALIDATION"];

    private static readonly Dictionary<string, string> Scopes = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = "All",
        ["all"] = "All",
        ["esas"] = "EsasNo",
        ["karar"] = "KararNo",
    };

    public string Source => "uyusmazlik";
    public string? RequiresKey => null;
    public bool SupportsFetch => true;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var scope = (ToolArgs.String(args, "scope") ?? "all").Trim();
        var scopeValue = Scopes.GetValueOrDefault(scope, "All");
        var page = Math.Max(1, ToolArgs.Int(args, "page") ?? 1);
        var limit = Math.Max(1, ToolArgs.Int(args, "limit") ?? 10);
        var caseSensitive = ToolArgs.Bool(args, "case_sensitive");

        var client = httpClientFactory.CreateClient("legal-insecure");

        var seed = await GetAsync(client, "/", ct);
        var searchFields = new List<KeyValuePair<string, string>>
        {
            new("txtSearch", query ?? ""),
            new("rblSearchScope", scopeValue),
            new("btnSearch", "Ara"),
        };
        if (caseSensitive)
        {
            searchFields.Add(new("chkCaseSensitive", "on"));
        }
        var current = await PostAsync(client, seed, searchFields, ct);

        // Walk server-side paging to the requested page, replaying fresh state each time.
        for (var target = 2; target <= page; target++)
        {
            var postback = PagePostback(current, target);
            if (postback is null)
            {
                break;
            }
            current = await PostAsync(client, current,
            [
                new("__EVENTTARGET", postback.Value.EventTarget),
                new("__EVENTARGUMENT", postback.Value.EventArgument),
                new("txtSearch", query ?? ""),
                new("rblSearchScope", scopeValue),
            ], ct);
        }

        return ParseRows(current).Take(limit).ToList();
    }

    public async Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
    {
        if (!id.StartsWith("http", StringComparison.OrdinalIgnoreCase) || !id.Contains("uyusmazlik.gov.tr", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var client = httpClientFactory.CreateClient("legal-insecure");
        using var resp = await client.GetAsync(id, ct);
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var markdown = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || id.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? LegalText.PdfToText(bytes)
            : LegalText.HtmlToMarkdown(Encoding.UTF8.GetString(bytes));
        return $"Kaynak: {id}\n\n{markdown}";
    }

    private static async Task<string> GetAsync(HttpClient client, string path, CancellationToken ct)
    {
        using var resp = await client.GetAsync($"{BaseUrl}{path}", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static async Task<string> PostAsync(
        HttpClient client, string seedHtml, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct)
    {
        var body = new List<KeyValuePair<string, string>>
        {
            new("__EVENTTARGET", ""),
            new("__EVENTARGUMENT", ""),
        };
        foreach (var (name, value) in State(seedHtml))
        {
            body.Add(new(name, value));
        }
        body.AddRange(fields);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/")
        {
            Content = new FormUrlEncodedContent(body),
        };
        request.Headers.Referrer = new Uri($"{BaseUrl}/");
        using var resp = await client.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static IEnumerable<KeyValuePair<string, string>> State(string html)
    {
        foreach (var name in StateFields)
        {
            var match = Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"");
            if (match.Success)
            {
                yield return new(name, WebUtility.HtmlDecode(match.Groups[1].Value));
            }
        }
    }

    private static (string EventTarget, string EventArgument)? PagePostback(string html, int targetPage)
    {
        var grid = Grid().Match(html);
        if (!grid.Success)
        {
            return null;
        }
        foreach (Match m in PageLink().Matches(grid.Value))
        {
            if (m.Groups[2].Value == $"Page${targetPage}")
            {
                return (WebUtility.HtmlDecode(m.Groups[1].Value), m.Groups[2].Value);
            }
        }
        return null;
    }

    private static List<SourceResult> ParseRows(string html)
    {
        var grid = Grid().Match(html);
        var results = new List<SourceResult>();
        if (!grid.Success)
        {
            return results;
        }
        foreach (Match tr in Row().Matches(grid.Value))
        {
            var cells = Cell().Matches(tr.Value).Select(c => Clean(c.Groups[1].Value)).ToList();
            if (cells.Count < 4)
            {
                continue;
            }
            var esasNo = cells[0];
            if (!CaseNo().IsMatch(esasNo.Replace(" ", string.Empty)))
            {
                continue; // header / pager row
            }
            var kararNo = cells[1];
            var pdf = PdfHref().Match(Cell().Matches(tr.Value).ElementAt(3).Groups[1].Value);
            var url = pdf.Success ? Absolute(WebUtility.HtmlDecode(pdf.Groups[1].Value)) : null;
            results.Add(new SourceResult(
                Source: "uyusmazlik",
                Id: url ?? (string.IsNullOrEmpty(kararNo) ? esasNo : kararNo),
                Title: string.IsNullOrEmpty(kararNo) ? esasNo : kararNo,
                Date: string.IsNullOrEmpty(cells[2]) ? null : cells[2],
                CaseNumber: esasNo,
                DecisionNumber: string.IsNullOrEmpty(kararNo) ? null : kararNo,
                Url: url));
        }
        return results;
    }

    private static string Absolute(string href) =>
        href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? href
            : new Uri(new Uri($"{BaseUrl}/"), href).ToString();

    private static string Clean(string html) =>
        WebUtility.HtmlDecode(Whitespace().Replace(Tag().Replace(html, " "), " ")).Trim();

    [GeneratedRegex(@"<table[^>]*id=""GridView1"".*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Grid();

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Row();

    [GeneratedRegex(@"<t[dh][^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Cell();

    [GeneratedRegex(@"href=['""]([^'""]+\.pdf)['""]", RegexOptions.IgnoreCase)]
    private static partial Regex PdfHref();

    [GeneratedRegex(@"__doPostBack\(&#39;([^&]+)&#39;,&#39;(Page\$\d+)&#39;\)")]
    private static partial Regex PageLink();

    [GeneratedRegex(@"^\d+/\d+$")]
    private static partial Regex CaseNo();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
