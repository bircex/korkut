using System.Text.Json;
using Korkut.Application.Runtime;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Law;

/// <summary>
/// Composite legal-search tool: searches a chosen public Turkish legal database. Dispatches by
/// <c>source</c> to the matching <see cref="ISourceClient"/>. Returns result metadata (ids, dates,
/// case/decision numbers); the model passes an id to <c>fetch_document</c> for the full text.
/// </summary>
public sealed class SearchDecisionsTool(IEnumerable<ISourceClient> clients, IEnvironment env) : IAgentTool
{
    private readonly Dictionary<string, ISourceClient> _bySource =
        clients.ToDictionary(c => c.Source, StringComparer.OrdinalIgnoreCase);

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "source": { "type": "string", "enum": ["yargitay","danistay","emsal","kyb","anayasa","mevzuat","gib","kik","rekabet","sayistay","uyusmazlik","bddk","kvkk","sigorta_tahkim"], "description": "Aranacak hukuki veritabanı" },
            "query": { "type": "string", "description": "Arama sorgusu" },
            "limit": { "type": "integer", "description": "Sonuç sayısı (varsayılan 10)" },
            "page": { "type": "integer", "description": "Sayfa (varsayılan 1)" },
            "date_start": { "type": "string", "description": "YYYY-MM-DD (yargitay/danistay/emsal/kyb/kik)" },
            "date_end": { "type": "string", "description": "YYYY-MM-DD" },
            "chamber": { "type": "string", "description": "Daire (yargitay/danistay)" },
            "decision_type": { "type": "string", "description": "rekabet/kik/sayistay için karar türü (ör. sayistay: genel_kurul/temyiz_kurulu/daire; kik: uyusmazlik/duzenleyici/mahkeme)" },
            "application_type": { "type": "string", "description": "anayasa için: norm veya individual" },
            "scope": { "type": "string", "description": "uyusmazlik için arama kapsamı: all/esas/karar" },
            "legislation_type": { "type": "string", "description": "mevzuat için tür filtresi (ör. KANUN, KHK, YONETMELIK)" }
          },
          "required": ["source", "query"]
        }
        """);

    public ToolSpec Spec => new("search_decisions",
        "Türk hukuki veritabanlarında (Yargıtay, Danıştay, Emsal, Mevzuat, Anayasa, GİB, KİK...) karar/mevzuat araması yapar.",
        Schema);

    public string? RequiresKey => null; // always offered; a source whose key is missing errors at runtime

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var source = ToolArgs.String(arguments, "source");
        var query = ToolArgs.String(arguments, "query");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Error("'source' ve 'query' gereklidir.");
        }

        if (!_bySource.TryGetValue(source, out var client))
        {
            return ToolResult.Error($"Desteklenmeyen kaynak: {source}");
        }

        if (client.RequiresKey is { } key && string.IsNullOrWhiteSpace(env.Get(key)))
        {
            return ToolResult.Error($"'{source}' kaynağı için API anahtarı yapılandırılmamış.");
        }

        try
        {
            var results = await client.SearchAsync(query.Trim(), arguments, ct);
            return ToolResult.Ok(ToolArgs.Result(results));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return ToolResult.Error($"'{source}' kaynağında arama başarısız oldu: {ex.Message}");
        }
    }
}
