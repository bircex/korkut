using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Composite legal-fetch tool: fetches the full text of a document returned by
/// <c>search_decisions</c>. Dispatches by <c>source</c> to the matching <see cref="ISourceClient"/>
/// and passes the <c>id</c> that source's search result gave.
/// </summary>
public sealed class FetchDocumentTool(IEnumerable<ISourceClient> clients, IEnvironment env) : IAgentTool
{
    private readonly Dictionary<string, ISourceClient> _bySource =
        clients.ToDictionary(c => c.Source, StringComparer.OrdinalIgnoreCase);

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "source": { "type": "string", "enum": ["yargitay","danistay","emsal","kyb","anayasa","mevzuat","gib","kik","rekabet","sayistay","uyusmazlik","bddk","kvkk","sigorta_tahkim"], "description": "Belgenin kaynağı (search_decisions ile aynı)" },
            "id": { "type": "string", "description": "search_decisions sonucundan gelen kimlik" },
            "article_id": { "type": "string", "description": "mevzuat için: yalnızca tek bir maddeyi getir" },
            "page": { "type": "integer", "description": "Uzun belgelerde sayfa (varsayılan 1)" },
            "page_size": { "type": "integer", "description": "Sayfa başına karakter (varsayılan 5000)" }
          },
          "required": ["source", "id"]
        }
        """);

    public ToolSpec Spec => new("fetch_document",
        "search_decisions'ten dönen bir hukuki belgenin tam metnini getirir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var source = ToolArgs.String(arguments, "source");
        var id = ToolArgs.String(arguments, "id");
        
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(id))
        {
            return ToolResult.Error("'source' ve 'id' gereklidir.");
        }

        if (!_bySource.TryGetValue(source, out var client))
        {
            return ToolResult.Error($"Desteklenmeyen kaynak: {source}");
        }

        if (!client.SupportsFetch)
        {
            return ToolResult.Error($"'{source}' kaynağı belge getirmeyi desteklemiyor.");
        }

        if (client.RequiresKey is { } key && string.IsNullOrWhiteSpace(env.Get(key)))
        {
            return ToolResult.Error($"'{source}' kaynağı için API anahtarı yapılandırılmamış.");
        }

        try
        {
            var text = await client.FetchAsync(id.Trim(), arguments, ct);
            return string.IsNullOrWhiteSpace(text)
                ? ToolResult.Ok("Belge bulunamadı.")
                : ToolResult.Ok(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        {
            return ToolResult.Error($"'{source}' belgesi getirilemedi: {ex.Message}");
        }
    }
}
