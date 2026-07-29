using Korkut.Core;
using System.Text;
using System.Text.Json;
using Korkut.Knowledge;
using Korkut.Tools.Core;

namespace Korkut.Tools;

/// <summary>
/// RAG tool: searches the office's OWN indexed documents via the Knowledge service and returns the
/// most relevant passages. A thin wrapper over <see cref="IKnowledgeSearchService"/> (which forwards the
/// caller's token to Korkut.Knowledge, so the office scope comes from the request, not the args).
/// </summary>
public sealed class KnowledgeSearchTool(IKnowledgeSearchService knowledge) : IAgentTool
{
    private const int DefaultTopK = 6;

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "Aranacak konu veya soru" },
            "top_k": { "type": "integer", "description": "Kaç alıntı dönsün (varsayılan 6)" }
          },
          "required": ["query"]
        }
        """);

    public ToolSpec Spec => new(
        "search_office_documents",
        "Büronun yüklü belgelerinde anlamsal arama yapar ve ilgili alıntıları döndürür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var query = arguments.TryGetProperty("query", out var q) ? q.GetString() : null;
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Error("'query' parametresi gereklidir.");
        }

        var topK = arguments.TryGetProperty("top_k", out var k) && k.TryGetInt32(out var n) ? n : DefaultTopK;

        var hits = await knowledge.SearchAsync(query, topK, ct);
        if (hits.Count == 0)
        {
            return ToolResult.Ok("İlgili belge bulunamadı.");
        }

        var sb = new StringBuilder();
        foreach (var hit in hits)
        {
            sb.Append("- ").AppendLine(hit.Text.ReplaceLineEndings(" ").Trim());
        }

        return ToolResult.Ok(sb.ToString().TrimEnd());
    }
}
