using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// One court-decision source served by the Ministry of Justice Bedesten API
/// (bedesten.adalet.gov.tr). A single endpoint covers several court types via
/// <c>itemTypeList</c>; each registered instance pins its own types (Yargıtay, Danıştay, Emsal,
/// KYB) and shares the search + full-text-fetch calls. Public portal; no API key. Ported 1:1 from
/// the Python <c>_base/bedesten.py</c> + the per-court <c>search_*</c> tools.
/// </summary>
public sealed class BedestenClient(
    IHttpClientFactory httpClientFactory,
    string source,
    IReadOnlyList<string> itemTypes,
    bool supportsChamber) : ISourceClient
{
    private const string SearchPath = "/emsal-karar/searchDocuments";
    
    private const string DocumentPath = "/emsal-karar/getDocumentContent";

    public string Source => source;
    public string? RequiresKey => null;
    public bool SupportsFetch => true;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
        => (await SearchPagedAsync(query, args, ct)).Items;

    public async Task<SearchOutcome> SearchPagedAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 10);
        var page = Math.Max(1, ToolArgs.Int(args, "page") ?? 1);

        var data = new Dictionary<string, object?>
        {
            ["pageSize"] = limit,
            ["pageNumber"] = page,
            ["itemTypeList"] = itemTypes,
            ["phrase"] = query,
        };

        // RELEVANCE is the default ordering: multi-word phrases match ANY word, so date ordering
        // returns the newest (mostly unrelated) hits while the engine's own scoring surfaces the
        // on-topic ones. Date ordering stays available via sort=date.
        if (string.Equals(ToolArgs.String(args, "sort"), "date", StringComparison.OrdinalIgnoreCase))
        {
            data["sortFields"] = new[] { "KARAR_TARIHI" };
            data["sortDirection"] = "desc";
        }

        if (supportsChamber && ToolArgs.String(args, "chamber") is { Length: > 0 } chamber)
        {
            data["birimAdi"] = chamber;
        }
        if (Iso(ToolArgs.String(args, "date_start"), end: false) is { } start)
        {
            data["kararTarihiStart"] = start;
        }
        if (Iso(ToolArgs.String(args, "date_end"), end: true) is { } end)
        {
            data["kararTarihiEnd"] = end;
        }

        var body = await PostJsonAsync(SearchPath,
            new { data, applicationName = "UyapMevzuat", paging = true }, ct);

        if (!body.RootElement.TryGetProperty("data", out var payload) ||
            !payload.TryGetProperty("emsalKararList", out var list) ||
            list.ValueKind != JsonValueKind.Array)
        {
            return new SearchOutcome([], Total: null);
        }

        // The database-reported TOTAL match count — surfaced so the assistant can report the real
        // corpus size ("2.894 karar uydu") instead of guessing from the 10 hits it pulled.
        long? total = payload.TryGetProperty("total", out var t) && t.TryGetInt64(out var n) ? n : null;

        var results = new List<SourceResult>(list.GetArrayLength());
        foreach (var e in list.EnumerateArray())
        {
            var documentId = Str(e, "documentId");
            if (documentId is null)
            {
                continue;
            }
            var court = Str(e, "birimAdi");
            results.Add(new SourceResult(
                Source: source,
                Id: documentId,
                Title: court ?? source,
                Date: Str(e, "kararTarihiStr") ?? Str(e, "kararTarihi"),
                CaseNumber: Str(e, "esasNo"),
                DecisionNumber: Str(e, "kararNo"),
                Court: court,
                Url: $"https://mevzuat.adalet.gov.tr/ictihat/{documentId}"));
        }
        return new SearchOutcome(results, total);
    }

    public async Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
    {
        var body = await PostJsonAsync(DocumentPath,
            new { data = new { documentId = id }, applicationName = "UyapMevzuat" }, ct);

        if (!body.RootElement.TryGetProperty("data", out var data))
        {
            return null;
        }
        var contentB64 = Str(data, "content");
        if (string.IsNullOrEmpty(contentB64))
        {
            return null;
        }

        var mime = Str(data, "mimeType") ?? string.Empty;
        var raw = Convert.FromBase64String(contentB64);
        var sourceUrl = $"https://mevzuat.adalet.gov.tr/ictihat/{id}";
        var markdown = mime switch
        {
            "text/html" => LegalText.HtmlToMarkdown(System.Text.Encoding.UTF8.GetString(raw)),
            "application/pdf" => LegalText.PdfToText(raw),
            _ => System.Text.Encoding.UTF8.GetString(raw),
        };
        return $"Kaynak: {sourceUrl}\n\n{markdown}";
    }

    private async Task<JsonDocument> PostJsonAsync(string path, object payload, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("bedesten");
        using var resp = await client.PostAsJsonAsync(path, payload, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;

    /// <summary>Accept YYYY-MM-DD and expand to the ISO 8601 form the API requires.</summary>
    private static string? Iso(string? date, bool end)
    {
        if (string.IsNullOrWhiteSpace(date) || date.EndsWith('Z'))
        {
            return date;
        }
        
        return end ? $"{date}T23:59:59.999Z" : $"{date}T00:00:00.000Z";
    }
}
