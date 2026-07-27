using Korkut.Application.Agent.Tools;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Korkut.Infrastructure.Agent.Tools.Law;

/// <summary>
/// KİK (Public Procurement Authority) board decisions from the legacy citizen portal
/// (ekap.kik.gov.tr). A classic ASP.NET WebForms grid: a GET seeds the <c>__VIEWSTATE</c> tokens +
/// session cookie, a POST replays them with the filter fields and the search button; each row's
/// preview postback reveals its <c>KararGoster</c> permalink. <c>decision_type</c> picks the
/// decision family. Legacy TLS host; search-only. Ported from Python <c>search_kik</c>.
/// </summary>
public sealed partial class KikClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string BaseUrl = "https://ekap.kik.gov.tr";
    private const string SearchPath = "/EKAP/Vatandas/KurulKararSorgu.aspx";
    private const string DocumentUrl = BaseUrl + "/EKAP/Vatandas/KararGoster.aspx?KararId=";
    private const string Prefix = "ctl00$ContentPlaceHolder1$";
    private const string SearchButton = Prefix + "btnAra";
    private const int PageSize = 10;

    private static readonly string[] StateFields =
    [
        "__VIEWSTATE", "__VIEWSTATEGENERATOR", "__VIEWSTATEENCRYPTED", "__EVENTVALIDATION",
        "__PIT", "__PITC", "__LASTFOCUS", "__SCROLLPOSITIONX", "__SCROLLPOSITIONY",
    ];

    private static readonly Dictionary<string, (string Radio, string Label)> DecisionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["uyusmazlik"] = ("rbUyusmazlik", "uyusmazlik"),
        ["duzenleyici"] = ("rbDuzenleyici", "duzenleyici"),
        ["mahkeme"] = ("rbMahkeme", "mahkeme"),
    };

    public string Source => "kik";
    public string? RequiresKey => null;
    public bool SupportsFetch => false;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var decisionType = (ToolArgs.String(args, "decision_type") ?? "uyusmazlik").Trim();
        if (!DecisionTypes.TryGetValue(decisionType, out var dt))
        {
            dt = DecisionTypes["uyusmazlik"];
        }
        var page = Math.Max(1, ToolArgs.Int(args, "page") ?? 1);
        var limit = Math.Max(1, ToolArgs.Int(args, "limit") ?? 10);

        var fields = FilterFields(dt.Radio, query,
            ToolArgs.String(args, "karar_no") ?? "",
            ToolArgs.String(args, "basvuran") ?? "",
            ToolArgs.String(args, "idare_adi") ?? "",
            ToolArgs.String(args, "subject") ?? "",
            ToolArgs.String(args, "date_start") ?? "",
            ToolArgs.String(args, "date_end") ?? "",
            ToolArgs.String(args, "year") ?? "");

        var client = httpClientFactory.CreateClient("legal-insecure");

        var seed = await GetAsync(client, ct);
        var current = await PostAsync(client, fields, seed, SearchButton, ct);

        for (var target = 2; target <= page; target++)
        {
            var pageTarget = PageTarget(current, target);
            if (pageTarget is null)
            {
                break;
            }
            current = await PostAsync(client, fields, current, pageTarget, ct);
        }

        var rows = ParseRows(current).Take(limit).ToList();
        var results = new List<SourceResult>(rows.Count);
        foreach (var row in rows)
        {
            var url = await ResolveUrlAsync(client, fields, current, row.Preview, ct);
            var title = !string.IsNullOrEmpty(row.Subject) ? row.Subject
                : !string.IsNullOrEmpty(row.Idare) ? row.Idare : row.KararNo;
            results.Add(new SourceResult(
                Source: "kik",
                Id: row.KararNo,
                Title: title,
                Date: IsoDate(row.Date),
                Court: dt.Label,
                Url: string.IsNullOrEmpty(url) ? null : url));
        }
        return results;
    }

    public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    private static List<KeyValuePair<string, string>> FilterFields(
        string radio, string query, string kararNo, string basvuran, string idareAdi,
        string subject, string dateStart, string dateEnd, string year) =>
    [
        new($"{Prefix}kurulKararTip", radio),
        new($"{Prefix}txtKararNo", kararNo),
        new($"{Prefix}etKararTarihBaslangic$EkapTakvimTextBox_etKararTarihBaslangic", dateStart),
        new($"{Prefix}etKararTarihBitis$EkapTakvimTextBox_etKararTarihBitis", dateEnd),
        new($"{Prefix}txtResmiGazeteSayisi", ""),
        new($"{Prefix}etResmiGazeteTarihi$EkapTakvimTextBox_etResmiGazeteTarihi", ""),
        new($"{Prefix}txtBasvuruKonusuIhale", subject),
        new($"{Prefix}txtSikayetci", basvuran),
        new($"{Prefix}txtIhaleyiYapanIdare", idareAdi),
        new($"{Prefix}ddlYil", string.IsNullOrEmpty(year) ? "0" : year),
        new($"{Prefix}txtKararMetni", query),
    ];

    private static async Task<string> GetAsync(HttpClient client, CancellationToken ct)
    {
        using var resp = await client.GetAsync($"{BaseUrl}{SearchPath}", ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static async Task<string> PostAsync(
        HttpClient client, List<KeyValuePair<string, string>> fields, string html, string eventTarget, CancellationToken ct)
    {
        var body = new List<KeyValuePair<string, string>>(fields);
        foreach (var (name, value) in State(html))
        {
            body.Add(new(name, value));
        }
        body.Add(new("__EVENTTARGET", eventTarget));
        body.Add(new("__EVENTARGUMENT", ""));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{SearchPath}")
        {
            Content = new FormUrlEncodedContent(body),
        };
        
        request.Headers.Referrer = new Uri($"{BaseUrl}{SearchPath}");
        request.Headers.Add("Origin", BaseUrl);
        
        using var resp = await client.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        
        var text = await resp.Content.ReadAsStringAsync(ct);
        
        if (text.Contains("Sayfada hata oluştu", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("KİK arama portalı hata sayfası döndürdü; form düzeni değişmiş olabilir.");
        }
        
        return text;
    }

    private static async Task<string> ResolveUrlAsync(
        HttpClient client, List<KeyValuePair<string, string>> fields, string html, string previewTarget, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(previewTarget))
        {
            return "";
        }
        try
        {
            var resp = await PostAsync(client, fields, html, previewTarget, ct);
            var match = KararIdIn().Match(resp);
            return match.Success ? $"{DocumentUrl}{match.Groups[1].Value}" : "";
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return "";
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> State(string html)
    {
        foreach (var name in StateFields)
        {
            var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
            if (match.Success)
            {
                yield return new(name, WebUtility.HtmlDecode(match.Groups[1].Value));
            }
        }
    }

    private static List<Row> ParseRows(string html)
    {
        var grid = Grid().Match(html);
        var rows = new List<Row>();
        if (!grid.Success)
        {
            return rows;
        }
        foreach (Match tr in RowRe().Matches(grid.Value))
        {
            var cells = Cell().Matches(tr.Value).Select(c => c.Groups[1].Value).ToList();
            if (cells.Count < 6)
            {
                continue;
            }
            var preview = Preview().Match(cells[0]);
            rows.Add(new Row(
                KararNo: Clean(cells[1]),
                Date: Clean(cells[2]),
                Idare: Clean(cells[3]),
                Subject: Clean(cells[5]),
                Preview: preview.Success ? WebUtility.HtmlDecode(preview.Groups[1].Value) : ""));
        }
        return rows;
    }

    private static string? PageTarget(string html, int targetPage)
    {
        var grid = Grid().Match(html);
        if (!grid.Success)
        {
            return null;
        }
        var pager = Pager().Match(grid.Value);
        if (!pager.Success)
        {
            return null;
        }
        foreach (Match m in PageLink().Matches(pager.Value))
        {
            if (int.TryParse(m.Groups[2].Value, out var num) && num == targetPage)
            {
                return WebUtility.HtmlDecode(m.Groups[1].Value);
            }
        }
        return null;
    }

    private static string IsoDate(string raw)
    {
        var m = DateRe().Match(raw);
        return m.Success ? $"{m.Groups[3].Value}-{m.Groups[2].Value}-{m.Groups[1].Value}" : raw;
    }

    private static string Clean(string html) =>
        WebUtility.HtmlDecode(Whitespace().Replace(Tag().Replace(html, " "), " ")).Trim();

    private sealed record Row(string KararNo, string Date, string Idare, string Subject, string Preview);

    [GeneratedRegex(@"<table[^>]*id=""grdKurulKararSorguSonuc"".*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Grid();

    [GeneratedRegex(@"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RowRe();

    [GeneratedRegex(@"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Cell();

    [GeneratedRegex(@"<tr[^>]*>(?:(?!</tr>).)*ctl14(?:(?!</tr>).)*</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Pager();

    [GeneratedRegex(@"__doPostBack\(&#39;([^&]+ctl14\$ctl\d+)&#39;,&#39;&#39;\)"">(\d+)<")]
    private static partial Regex PageLink();

    [GeneratedRegex(@"__doPostBack\(&#39;([^&]+btnOnizle)&#39;")]
    private static partial Regex Preview();

    [GeneratedRegex(@"KararGoster\.aspx\?KararId=([0-9a-fA-F]+)")]
    private static partial Regex KararIdIn();

    [GeneratedRegex(@"(\d{2})\.(\d{2})\.(\d{4})")]
    private static partial Regex DateRe();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
