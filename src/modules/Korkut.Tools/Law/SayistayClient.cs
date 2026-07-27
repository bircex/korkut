using System.Text.Json;
using Korkut.Application.Agent.Tools;
using System.Text.RegularExpressions;

namespace Korkut.Tools.Law;

/// <summary>
/// Sayıştay (Court of Accounts) decisions. Three decision families, each behind an ASP.NET Core MVC
/// server-side DataTables grid: a GET seeds the antiforgery cookie + token, a POST replays the token
/// with the DataTables payload and the family's filter fields. <c>decision_type</c> selects the
/// family. Search-only, no API key. Ported from Python <c>search_sayistay</c>.
/// </summary>
public sealed partial class SayistayClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string WafMarker = "Bilgi Güvenliği Politikaları Gereği Kısıtlanmıştır";
    private const int MaxLength = 10;

    private static readonly Dictionary<string, string> Page = new()
    {
        ["genel_kurul"] = "/KararlarGenelKurul",
        ["temyiz_kurulu"] = "/KararlarTemyiz",
        ["daire"] = "/KararlarDaire",
    };

    private static readonly Dictionary<string, string> SearchPath = new()
    {
        ["genel_kurul"] = "/KararlarGenelKurul/DataTablesList",
        ["temyiz_kurulu"] = "/KararlarTemyiz/DataTablesList",
        ["daire"] = "/KararlarDaire/DataTablesList",
    };

    private static readonly Dictionary<string, string[]> Columns = new()
    {
        ["genel_kurul"] = ["KARARNO", "KARARNO", "KARARTARIH", "KARAROZETI", ""],
        ["temyiz_kurulu"] = ["TEMYIZTUTANAKTARIHI", "TEMYIZTUTANAKTARIHI", "ILAMDAIRESI", "TEMYIZKARAR", ""],
        ["daire"] = ["YARGILAMADAIRESI", "KARARTRH", "KARARNO", "YARGILAMADAIRESI", "WEBKARARMETNI", ""],
    };

    private static readonly Dictionary<string, int> OrderColumn = new()
    {
        ["genel_kurul"] = 2,
        ["temyiz_kurulu"] = 0,
        ["daire"] = 1,
    };

    private static readonly Dictionary<string, string> TextField = new()
    {
        ["genel_kurul"] = "KararlarGenelKurulAra.KARARTAMAMI",
        ["temyiz_kurulu"] = "KararlarTemyizAra.TEMYIZKARAR",
        ["daire"] = "KararlarDaireAra.WEBKARARMETNI",
    };

    private static readonly Dictionary<string, string[]> EmptyFields = new()
    {
        ["genel_kurul"] =
        [
            "KararlarGenelKurulAra.KARARNO", "KararlarGenelKurulAra.KARAREK",
            "KararlarGenelKurulAra.KARARTARIHBaslangic", "KararlarGenelKurulAra.KARARTARIHBitis",
        ],
        ["temyiz_kurulu"] =
        [
            "KararlarTemyizAra.DOSYANO", "KararlarTemyizAra.ILAMDAIRESI", "KararlarTemyizAra.ILAMNO",
            "KararlarTemyizAra.KAMUIDARESITURU", "KararlarTemyizAra.KARARTRHBaslangic",
            "KararlarTemyizAra.KARARTRHBitis", "KararlarTemyizAra.TEMYIZTUTANAKNO",
            "KararlarTemyizAra.WEBKARARKONUSU", "KararlarTemyizAra.YILI",
        ],
        ["daire"] =
        [
            "KararlarDaireAra.HESAPYILI", "KararlarDaireAra.ILAMNO", "KararlarDaireAra.KAMUIDARESITURU",
            "KararlarDaireAra.KARARTRHBaslangic", "KararlarDaireAra.KARARTRHBitis",
            "KararlarDaireAra.WEBKARARKONUSU", "KararlarDaireAra.YARGILAMADAIRESI",
        ],
    };

    public string Source => "sayistay";
    public string? RequiresKey => null;
    public bool SupportsFetch => false;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var decisionType = (ToolArgs.String(args, "decision_type") ?? "genel_kurul").Trim();
        if (!Page.ContainsKey(decisionType))
        {
            decisionType = "genel_kurul";
        }
        var length = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, MaxLength);
        var start = Math.Max(0, (Math.Max(1, ToolArgs.Int(args, "page") ?? 1) - 1) * length);

        var client = httpClientFactory.CreateClient("legal");

        // GET the search page to seed the antiforgery cookie and read the token.
        using var pageResp = await client.GetAsync($"https://www.sayistay.gov.tr{Page[decisionType]}", ct);
        var pageHtml = await pageResp.Content.ReadAsStringAsync(ct);
        if (pageHtml.Contains(WafMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Sayıştay isteği WAF tarafından engellendi; daha sonra deneyin.");
        }
        var tokenMatch = Csrf().Match(pageHtml);
        if (!tokenMatch.Success)
        {
            throw new InvalidOperationException("Sayıştay antiforgery token'ı bulunamadı; sayfa düzeni değişmiş olabilir.");
        }

        var fields = BuildFields(decisionType, query, start, length, tokenMatch.Groups[1].Value);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://www.sayistay.gov.tr{SearchPath[decisionType]}")
        {
            Content = new FormUrlEncodedContent(fields),
        };
        request.Headers.Referrer = new Uri($"https://www.sayistay.gov.tr{Page[decisionType]}");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");

        using var resp = await client.SendAsync(request, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (text.Contains(WafMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Sayıştay arama isteği WAF tarafından engellendi; daha sonra deneyin.");
        }

        using var body = JsonDocument.Parse(text);
        if (!body.RootElement.TryGetProperty("data", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<SourceResult>(rows.GetArrayLength());
        foreach (var row in rows.EnumerateArray())
        {
            results.Add(Map(decisionType, row));
        }
        return results;
    }

    public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    private static List<KeyValuePair<string, string>> BuildFields(
        string decisionType, string query, int start, int length, string token)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("draw", "1"),
            new("start", start.ToString()),
            new("length", length.ToString()),
            new("search[value]", ""),
            new("search[regex]", "false"),
            new("order[0][column]", OrderColumn[decisionType].ToString()),
            new("order[0][dir]", "desc"),
        };
        var columns = Columns[decisionType];
        for (var i = 0; i < columns.Length; i++)
        {
            fields.Add(new($"columns[{i}][data]", columns[i]));
            fields.Add(new($"columns[{i}][name]", ""));
            fields.Add(new($"columns[{i}][searchable]", "true"));
            fields.Add(new($"columns[{i}][orderable]", "true"));
            fields.Add(new($"columns[{i}][search][value]", ""));
            fields.Add(new($"columns[{i}][search][regex]", "false"));
        }
        fields.Add(new("__RequestVerificationToken", token));
        fields.Add(new(TextField[decisionType], query));
        foreach (var name in EmptyFields[decisionType])
        {
            fields.Add(new(name, ""));
        }
        return fields;
    }

    private static SourceResult Map(string decisionType, JsonElement row)
    {
        var id = Str(row, "Id");
        var url = $"https://www.sayistay.gov.tr/{Page[decisionType].TrimStart('/')}/Detay/{id}/";
        var label = decisionType switch
        {
            "temyiz_kurulu" => "Temyiz Kurulu",
            "daire" => "Daire",
            _ => "Genel Kurul",
        };
        var (title, date) = decisionType switch
        {
            "temyiz_kurulu" => (
                Str(row, "ILAMDAIRESI") is { Length: > 0 } d ? $"{d}. Daire" : Str(row, "TEMYIZKARAR") ?? "",
                Str(row, "TEMYIZTUTANAKTARIHI") ?? ""),
            "daire" => ((Str(row, "KARARNO") ?? Str(row, "ILAMNO") ?? "").Trim(), Str(row, "KARARTRH") ?? ""),
            _ => ((Str(row, "KARARNO") ?? "").Trim(), Str(row, "KARARTARIH") ?? ""),
        };
        return new SourceResult(
            Source: "sayistay",
            Id: id ?? "",
            Title: string.IsNullOrEmpty(title) ? (id ?? "sayistay") : title,
            Date: string.IsNullOrEmpty(date) ? null : date,
            Court: label,
            Url: url);
    }

    private static string? Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
        {
            return null;
        }
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.ToString(),
            _ => null,
        };
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex Csrf();
}
