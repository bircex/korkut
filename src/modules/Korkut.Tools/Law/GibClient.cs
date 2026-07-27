using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Application.Agent.Tools;

namespace Korkut.Tools.Law;

/// <summary>
/// GİB (Revenue Administration) tax rulings (özelge) from the public gib.gov.tr JSON API. The list
/// endpoint takes pagination/sort as query-string params and the free-text filter in the JSON body
/// (ORed across title, law number and body text). Public; no API key; search-only. Ported from
/// Python <c>search_gib</c>.
/// </summary>
public sealed class GibClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string ListEndpoint = "https://gib.gov.tr/api/gibportal/mevzuat/ozelge/list";

    public string Source => "gib";
    public string? RequiresKey => null;
    public bool SupportsFetch => false;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string? query, JsonElement args, CancellationToken ct = default)
    {
        var limit = Math.Max(1, ToolArgs.Int(args, "limit") ?? 10);
        var page = Math.Max(0, (ToolArgs.Int(args, "page") ?? 1) - 1); // API pages are 0-indexed

        var body = new Dictionary<string, object?>
        {
            ["status"] = 2,
            ["deleted"] = false,
            ["ktype"] = 99,
        };
        var term = (query ?? string.Empty).Trim();
        
        if (term.Length > 0)
        {
            body["title"] = term;
            body["kanunNo"] = term;
            body["description"] = term;
        }
        if (ToIso(ToolArgs.String(args, "date_start"), endOfDay: false) is { } start)
        {
            body["ozelgeStartDate"] = start;
        }
        if (ToIso(ToolArgs.String(args, "date_end"), endOfDay: true) is { } end)
        {
            body["ozelgeEndDate"] = end;
        }

        var url = $"{ListEndpoint}?page={page}&size={limit}&sortFieldName=ozelgeTarih&sortType=DESC";
        var client = httpClientFactory.CreateClient("legal");
        using var resp = await client.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("resultContainer", out var container) ||
            !container.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<SourceResult>(content.GetArrayLength());
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var idEl) ||
                idEl.ValueKind is JsonValueKind.Null)
            {
                continue;
            }
            
            results.Add(new SourceResult(
                Source: "gib",
                Id: idEl.ToString(),
                Title: Str(item, "title") ?? string.Empty,
                Date: Str(item, "ozelgeTarih"),
                Court: "ozelge",
                Url: Str(item, "siteLink")));
        }
        return results;
    }

    public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    /// <summary>Normalize a date to full ISO 8601; the backend rejects date-only strings.</summary>
    private static string? ToIso(string? value, bool endOfDay)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        if (text.Contains('T'))
        {
            return text;
        }
        return endOfDay ? $"{text}T23:59:59.999Z" : $"{text}T00:00:00.000Z";
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;
}
