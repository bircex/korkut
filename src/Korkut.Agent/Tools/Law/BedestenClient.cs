using System.Text.Json;

namespace Korkut.Agent.Tools.Law;

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
    {
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 10);
        var page = Math.Max(1, ToolArgs.Int(args, "page") ?? 1);

        var data = new Dictionary<string, object?>
        {
            ["pageSize"] = limit,
            ["pageNumber"] = page,
            ["itemTypeList"] = itemTypes,
            ["phrase"] = query,
            ["sortFields"] = new[] { "KARAR_TARIHI" },
            ["sortDirection"] = "desc",
        };
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
            return [];
        }

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
        return results;
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
