using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Law rationale (gerekçe): the published reasoning behind a law — general rationale, committee
/// reports and article-by-article justifications. Accepts the law-level <c>gerekce_id</c> from a
/// <c>search_decisions</c> source=mevzuat result or an article-level one from
/// <c>get_mevzuat_madde_tree</c>. Long rationales are paged like <c>fetch_document</c>. Ported
/// from the Python <c>get_mevzuat_gerekce</c>.
/// </summary>
public sealed class GetMevzuatGerekceTool(MevzuatClient client) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "gerekce_id": { "type": "string", "description": "Gerekçe kimliği: search_decisions source=mevzuat sonucundaki gerekce_id (kanun geneli) veya get_mevzuat_madde_tree'deki gerekce_id (tek madde)" },
            "page": { "type": "integer", "description": "Uzun gerekçelerde sayfa (varsayılan 1)" },
            "page_size": { "type": "integer", "description": "Sayfa başına karakter (varsayılan 5000)" }
          },
          "required": ["gerekce_id"]
        }
        """);

    public ToolSpec Spec => new("get_mevzuat_gerekce",
        "Bir kanunun gerekçesini (kanunun amacı, komisyon raporları, madde gerekçeleri) getirir. Yalnızca gerekçesi yayımlanmış mevzuat için; arama sonucunda veya madde ağacında gerekce_id varsa kullanılabilir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var gerekceId = ToolArgs.String(arguments, "gerekce_id");
        if (string.IsNullOrWhiteSpace(gerekceId))
        {
            return ToolResult.Error("'gerekce_id' gereklidir.");
        }

        (string? MevzuatId, string Text)? gerekce;
        try
        {
            gerekce = await client.GetGerekceAsync(gerekceId.Trim(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        {
            return ToolResult.Error($"Gerekçe getirilemedi: {ex.Message}");
        }
        if (gerekce is not { } found)
        {
            return ToolResult.Error($"'{gerekceId}' için yayımlanmış bir gerekçe bulunamadı.");
        }

        var page = ToolArgs.Int(arguments, "page") ?? 1;
        var pageSize = Math.Clamp(ToolArgs.Int(arguments, "page_size") ?? 5000, 500, 20000);
        var (chunk, actualPage, totalPages) = LegalText.ChunkMarkdown(found.Text, page, pageSize);

        var header = found.MevzuatId is { } mevzuatId
            ? $"Gerekçe {gerekceId} (mevzuat {mevzuatId}), sayfa {actualPage}/{totalPages}"
            : $"Gerekçe {gerekceId}, sayfa {actualPage}/{totalPages}";
        return ToolResult.Ok($"{header}\n\n{chunk}");
    }
}
