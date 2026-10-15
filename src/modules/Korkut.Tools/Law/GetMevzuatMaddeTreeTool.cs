using System.Text;
using Korkut.Contract.LawClients;
using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Table of contents (madde ağacı / içindekiler) of one piece of legislation: nested kısım/bölüm
/// headings with MADDE leaves, each carrying the <c>maddeId</c> that <c>fetch_document</c>'s
/// <c>article_id</c> accepts and, where published, an article-level <c>gerekceId</c> for
/// <c>get_mevzuat_gerekce</c>. Use it to map a large law before pulling individual articles. Thin
/// adapter over <see cref="ILawClientsDocumentService"/> — renders the typed tree it returns as an
/// indented outline for the model.
/// </summary>
public sealed class GetMevzuatMaddeTreeTool(ILawClientsDocumentService documents) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "mevzuat_id": { "type": "string", "description": "Mevzuat kimliği (search_decisions source=mevzuat sonucundaki id; kanun numarası DEĞİL)" }
          },
          "required": ["mevzuat_id"]
        }
        """);

    public ToolSpec Spec => new("get_mevzuat_madde_tree",
        "Bir mevzuatın madde ağacını (içindekiler: kısım/bölüm/madde) döner. Her madde satırındaki madde_id, fetch_document'in article_id parametresiyle tek maddeyi getirmekte; gerekce_id ise get_mevzuat_gerekce ile madde gerekçesini almakta kullanılır. KANUN/KHK/TUZUK gibi yapılandırılmış türlerde çalışır; genelge/tebliğlerde boş dönebilir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.ResearchUse;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var mevzuatId = ToolArgs.String(arguments, "mevzuat_id");
        if (string.IsNullOrWhiteSpace(mevzuatId))
        {
            return ToolResult.Error("'mevzuat_id' gereklidir.");
        }

        LawClientsMaddeTreeResponse tree;
        try
        {
            tree = await documents.GetMaddeTreeAsync(mevzuatId.Trim(), ct);
        }
        catch (Exception ex) when (ex is BadRequestException or NotFoundException)
        {
            return ToolResult.Error(ex.Message);
        }

        if (tree.Count == 0)
        {
            return ToolResult.Ok(
                $"'{mevzuatId}' için madde ağacı yayımlanmamış (genelge/tebliğ türlerinde olağandır); metni fetch_document veya search_within_mevzuat ile alın.");
        }

        var sb = new StringBuilder();
        foreach (var node in tree.Nodes)
        {
            AppendNode(sb, node, 0);
        }
        return ToolResult.Ok($"Mevzuat {mevzuatId} madde ağacı ({tree.Count} düğüm):\n\n{sb}");
    }

    private static void AppendNode(StringBuilder sb, LawClientsMaddeTreeNode node, int depth)
    {
        // Kitap/kısım/bölüm headings also carry a maddeId, so the leaf test is the article number.
        var label = node.MaddeNo is { } maddeNo
            ? $"MADDE {maddeNo}: {node.Title} [madde_id={node.MaddeId}"
                + (node.GerekceId is { } gerekce ? $", gerekce_id={gerekce}]" : "]")
            : node.Title;

        if (label is not null)
        {
            sb.Append(new string(' ', depth * 2)).AppendLine(label);
        }

        foreach (var child in node.Children)
        {
            AppendNode(sb, child, label is null ? depth : depth + 1);
        }
    }
}
