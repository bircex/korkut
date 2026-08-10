using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Core;
using Korkut.LawClients.Services.Shared;

namespace Korkut.LawClients.Services.Sources;

/// <summary>
/// KİK (Public Procurement Authority) board decisions — proxied through the standalone
/// Korkut.KikProxy service (KORKUT_KIK_PROXY_URL) instead of calling ekap.kik.gov.tr in-process.
/// That legacy host's TLS handshake only completes under a relaxed OpenSSL SECLEVEL, which must
/// not apply to this process (it would weaken every other outbound HTTPS call the Api/Agent host
/// makes, including to the model providers) — see docker/Dockerfile.kikproxy for the actual
/// relaxation, scoped to that single-purpose container. Search-only: KİK never exposed a
/// full-text fetch, in-process or proxied.
/// </summary>
internal sealed class KikProxyClient(IHttpClientFactory httpClientFactory, IEnvironment env) : ISourceClient
{
    // Minimal APIs (Korkut.KikProxy/Program.cs) serialize with the ASP.NET "Web" defaults
    // (camelCase); GetFromJsonAsync's own default JsonSerializerOptions is case-SENSITIVE
    // PascalCase, so without this every property here would silently deserialize to null.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Source => "kik";
    public string? RequiresKey => null;
    public bool SupportsFetch => false;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var baseUrl = env.Get("KORKUT_KIK_PROXY_URL")?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("KORKUT_KIK_PROXY_URL yapılandırılmamış.");
        }

        var qs = new List<string> { $"query={Uri.EscapeDataString(query)}" };
        void Add(string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                qs.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }
        Add("decisionType", ToolArgs.String(args, "decision_type"));
        Add("page", ToolArgs.Int(args, "page")?.ToString());
        Add("limit", ToolArgs.Int(args, "limit")?.ToString());
        Add("kararNo", ToolArgs.String(args, "karar_no"));
        Add("basvuran", ToolArgs.String(args, "basvuran"));
        Add("idareAdi", ToolArgs.String(args, "idare_adi"));
        Add("subject", ToolArgs.String(args, "subject"));
        Add("dateStart", ToolArgs.String(args, "date_start"));
        Add("dateEnd", ToolArgs.String(args, "date_end"));
        Add("year", ToolArgs.String(args, "year"));

        var client = httpClientFactory.CreateClient("kik-proxy");
        var response = await client.GetFromJsonAsync<ProxyResponse>(
            $"{baseUrl.TrimEnd('/')}/search?{string.Join('&', qs)}", Json, ct);

        return response?.Results is { } results
            ? results.Select(r => new SourceResult(
                Source: "kik", Id: r.Id, Title: r.Title, Date: r.Date, Court: r.Court, Url: r.Url)).ToList()
            : [];
    }

    public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    private sealed record ProxyResponse(List<ProxyResult>? Results);

    private sealed record ProxyResult(string Id, string Title, string? Date, string? Court, string? Url);
}
