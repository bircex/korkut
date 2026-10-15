using Korkut.Contract.LawClients;
using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Composite legal-fetch tool: fetches the full text of a document returned by
/// <c>search_decisions</c>. Thin adapter over <see cref="ILawClientsDocumentService"/> — the same
/// service the UI's İlgili Kararlar drawer uses.
/// </summary>
public sealed class FetchDocumentTool(ILawClientsDocumentService documents) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "source": { "type": "string", "enum": ["yargitay","danistay","emsal","kyb","anayasa","mevzuat","gib","kik","rekabet","sayistay","uyusmazlik","bddk","kvkk","sigorta_tahkim"], "description": "Belgenin kaynağı (search_decisions ile aynı)" },
            "id": { "type": "string", "description": "search_decisions sonucundan gelen kimlik" },
            "article_id": { "type": "string", "description": "mevzuat için: yalnızca tek bir maddeyi getir (madde_id, get_mevzuat_madde_tree sonucundan)" },
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

    public string? RequiredPermission => Permissions.ResearchUse;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var source = ToolArgs.String(arguments, "source");
        var id = ToolArgs.String(arguments, "id");

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(id))
        {
            return ToolResult.Error("'source' ve 'id' gereklidir.");
        }

        try
        {
            var doc = await documents.GetAsync(
                source.Trim(), id.Trim(), ToolArgs.String(arguments, "article_id"), ToolArgs.Int(arguments, "page"), ct);
            return ToolResult.Ok(doc.ContentMd);
        }
        catch (Exception ex) when (ex is BadRequestException or NotFoundException)
        {
            return ToolResult.Error(ex.Message);
        }
    }
}
