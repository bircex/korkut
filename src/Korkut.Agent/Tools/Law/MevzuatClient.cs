using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Agent.Tools;

namespace Korkut.Agent.Tools.Law;

/// <summary>
/// Turkish legislation (mevzuat) search over the Ministry of Justice UYAP Mevzuat Bedesten
/// endpoint (<c>/mevzuat/searchDocuments</c>): laws, decree-laws, statutes, regulations,
/// Presidential decrees/decisions/circulars, communiqués. Public; no API key. Ported from the
/// Python <c>search_mevzuat</c>. Full-text fetch (fetch_legislation) is added with the other
/// sources, so this source is search-only for now.
/// </summary>
public sealed class MevzuatClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string SearchPath = "/mevzuat/searchDocuments";

    public string Source => "mevzuat";
    public string? RequiresKey => null;
    public bool SupportsFetch => false;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 50);
        var page = Math.Max(1, ToolArgs.Int(args, "page") ?? 1);

        var data = new Dictionary<string, object?>
        {
            ["pageSize"] = limit,
            ["pageNumber"] = page,
            ["sortFields"] = new[] { "RESMI_GAZETE_TARIHI" },
            ["sortDirection"] = "desc",
        };
        if (!string.IsNullOrWhiteSpace(query))
        {
            data["mevzuatAdi"] = query;
        }
        var types = (ToolArgs.String(args, "legislation_type") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .ToArray();
        if (types.Length > 0)
        {
            data["mevzuatTurList"] = types;
        }

        var client = httpClientFactory.CreateClient("bedesten");
        using var resp = await client.PostAsJsonAsync(SearchPath,
            new { data, applicationName = "UyapMevzuat", paging = true }, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var body = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!body.RootElement.TryGetProperty("data", out var payload) ||
            !payload.TryGetProperty("mevzuatList", out var list) ||
            list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<SourceResult>(list.GetArrayLength());
        foreach (var e in list.EnumerateArray())
        {
            var id = Str(e, "mevzuatId");
            if (id is null)
            {
                continue;
            }
            results.Add(new SourceResult(
                Source: "mevzuat",
                Id: id,
                Title: Str(e, "mevzuatAdi") ?? id,
                Date: Str(e, "resmiGazeteTarihi"),
                DecisionNumber: Str(e, "mevzuatNo"),
                Court: LegislationType(e),
                Url: $"https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId={id}"));
        }
        return results;
    }

    public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    private static string? LegislationType(JsonElement entry)
    {
        if (!entry.TryGetProperty("mevzuatTur", out var tur))
        {
            return null;
        }
        return tur.ValueKind switch
        {
            JsonValueKind.String => tur.GetString(),
            JsonValueKind.Object => Str(tur, "name") ?? Str(tur, "description"),
            _ => null,
        };
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() :
        e.TryGetProperty(name, out var n) && n.ValueKind is JsonValueKind.Number ? n.ToString() : null;
}
