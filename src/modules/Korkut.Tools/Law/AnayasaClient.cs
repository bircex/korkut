using Korkut.Tools.Tools;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;

namespace Korkut.Tools.Law;

/// <summary>
/// Constitutional Court (Anayasa Mahkemesi) decisions from the public "Kararlar Bilgi Bankası"
/// JSON API. One endpoint covers both corpora via a <c>kararTipi</c> discriminator, selected by the
/// <c>application_type</c> arg: "norm" (norm denetimi) or "individual" (bireysel başvuru). Public;
/// no API key; search-only. Ported from Python <c>search_anayasa</c>.
/// </summary>
public sealed class AnayasaClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string PortalBase = "https://normkararlarbilgibankasi.anayasa.gov.tr";
    
    private const string SearchEndpoint = PortalBase + "/api/core/public/search";
    
    private const string DetailPath = "/kbb/pages/search";
    
    private const string IdSeed = "kbb";
    public string Source => "anayasa";
    public string? RequiresKey => null;
    public bool SupportsFetch => false;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var mode = (ToolArgs.String(args, "application_type") ?? "norm").Trim().ToLowerInvariant();
        var kararTipi = mode == "individual" ? "BireyselBasvuru" : "NormDenetimi";
        var limit = Math.Max(1, ToolArgs.Int(args, "limit") ?? 10);
        var apiPage = Math.Max(1, ToolArgs.Int(args, "page") ?? 1) - 1;

        var payload = new
        {
            query,
            page = apiPage,
            size = limit,
            sort = "yayinTarihi",
            order = "desc",
            kararTipi,
        };

        var client = httpClientFactory.CreateClient("legal");
        using var resp = await client.PostAsJsonAsync(SearchEndpoint, payload, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var body = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!body.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<SourceResult>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            var itemTipi = Str(item, "kararTipi") ?? kararTipi;
            var id = item.TryGetProperty("id", out var idEl) && idEl.ValueKind is not JsonValueKind.Null
                ? idEl.ToString()
                : null;
            results.Add(new SourceResult(
                Source: "anayasa",
                Id: id ?? string.Empty,
                Title: Title(item, itemTipi) ?? id ?? "anayasa",
                Date: DecisionDate(item),
                Court: itemTipi,
                Url: DetailUrl(id, itemTipi)));
        }
        return results;
    }

    public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    private static string? Title(JsonElement item, string kararTipi)
    {
        if (kararTipi == "BireyselBasvuru")
        {
            var name = (Str(item, "basvuruAdi") ?? string.Empty).Trim();
            
            var basvuruNo = (Str(item, "basvuruNo") ?? string.Empty).Trim();
            
            if (name.Length > 0 && basvuruNo.Length > 0)
            {
                return $"{name} (B. No: {basvuruNo})";
            }
            
            return name.Length > 0 ? name : basvuruNo.Length > 0 ? basvuruNo : null;
        }
        
        var esas = (Str(item, "esasNo") ?? string.Empty).Trim();
        
        var karar = (Str(item, "kararNo") ?? string.Empty).Trim();
        
        if (esas.Length > 0 && karar.Length > 0)
        {
            return $"E. {esas}, K. {karar}";
        }
        
        return esas.Length > 0 ? esas : karar.Length > 0 ? karar : null;
    }

    private static string? DecisionDate(JsonElement item)
    {
        var date = Str(item, "kararTarihi") ?? Str(item, "yayinTarihi");
        
        return date?.Split('T', 2)[0];
    }

    private static string? DetailUrl(string? decisionId, string kararTipi)
    {
        if (string.IsNullOrEmpty(decisionId))
        {
            return null;
        }
        
        var raw = Encoding.UTF8.GetBytes($"{IdSeed}:{decisionId}");
        var hashed = Convert.ToBase64String(raw).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var q = $"id={Uri.EscapeDataString(hashed)}&type={Uri.EscapeDataString(kararTipi)}";
        
        return $"{PortalBase}{DetailPath}/{kararTipi}?{q}";
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;
}
