using Korkut.Core;
using System.Text;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Table of contents (madde ağacı / içindekiler) of one piece of legislation: nested kısım/bölüm
/// headings with MADDE leaves, each carrying the <c>maddeId</c> that <c>fetch_document</c>'s
/// <c>article_id</c> accepts and, where published, an article-level <c>gerekceId</c> for
/// <c>get_mevzuat_gerekce</c>. Use it to map a large law before pulling individual articles.
/// Ported from the Python <c>get_mevzuat_madde_tree</c>.
/// </summary>
public sealed class GetMevzuatMaddeTreeTool(MevzuatClient client) : IAgentTool
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

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var mevzuatId = ToolArgs.String(arguments, "mevzuat_id");
        if (string.IsNullOrWhiteSpace(mevzuatId))
        {
            return ToolResult.Error("'mevzuat_id' gereklidir.");
        }

        JsonDocument? tree;
        try
        {
            tree = await client.GetMaddeTreeAsync(mevzuatId.Trim(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return ToolResult.Error($"Madde ağacı getirilemedi: {ex.Message}");
        }

        using (tree)
        {
            var (text, count) = tree is null ? (string.Empty, 0) : Format(tree.RootElement);
            if (count == 0)
            {
                return ToolResult.Ok(
                    $"'{mevzuatId}' için madde ağacı yayımlanmamış (genelge/tebliğ türlerinde olağandır); metni fetch_document veya search_within_mevzuat ile alın.");
            }
            return ToolResult.Ok($"Mevzuat {mevzuatId} madde ağacı ({count} düğüm):\n\n{text}");
        }
    }

    /// <summary>Render the tree response's <c>children</c> as an indented outline; returns the text
    /// and the number of nodes rendered.</summary>
    public static (string Text, int Count) Format(JsonElement root)
    {
        var sb = new StringBuilder();
        var count = 0;
        if (root.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                AppendNode(sb, child, 0, ref count);
            }
        }
        return (sb.ToString(), count);
    }

    private static void AppendNode(StringBuilder sb, JsonElement node, int depth, ref int count)
    {
        // Kitap/kısım/bölüm headings also carry a maddeId, so the leaf test is the article number.
        var label = Str(node, "maddeNo") is { } maddeNo
            ? $"MADDE {maddeNo}: {Str(node, "title") ?? Str(node, "maddeBaslik") ?? "(başlıksız)"} [madde_id={Str(node, "maddeId")}"
                + (Str(node, "gerekceId") is { } gerekce ? $", gerekce_id={gerekce}]" : "]")
            : Str(node, "title") ?? Str(node, "description") ?? Str(node, "maddeBaslik");

        if (label is not null)
        {
            sb.Append(new string(' ', depth * 2)).AppendLine(label);
            count++;
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                AppendNode(sb, child, label is null ? depth : depth + 1, ref count);
            }
        }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() :
        e.TryGetProperty(name, out var n) && n.ValueKind is JsonValueKind.Number ? n.ToString() : null;
}
