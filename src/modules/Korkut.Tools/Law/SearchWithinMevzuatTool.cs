using Korkut.Contract.LawClients;
using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Search inside one piece of legislation at article level: splits the full text into MADDE blocks
/// and returns the articles matching a boolean keyword query, ranked by match count with full
/// article text. The right tool for large laws (TCK, TTK...) where <c>fetch_document</c> would
/// return far too much. Thin adapter over <see cref="ILawClientsSearchService"/>.
/// </summary>
public sealed class SearchWithinMevzuatTool(ILawClientsSearchService search) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "mevzuat_id": { "type": "string", "description": "Mevzuat kimliği (search_decisions source=mevzuat sonucundaki id; kanun numarası DEĞİL)" },
            "keyword": { "type": "string", "description": "Arama sorgusu. Operatörler BÜYÜK harf: 'yatırımcı AND tazmin' (ikisi de), 'vergi OR harç' (en az biri), 'yatırımcı NOT kurum' (hariç), '\"mali sıkıntı\"' (tam ifade), birleşik: '\"mali sıkıntı\" AND yatırımcı NOT kurum'" },
            "case_sensitive": { "type": "boolean", "description": "Büyük/küçük harf duyarlı eşleşme (varsayılan false)" },
            "max_results": { "type": "integer", "description": "En fazla eşleşen madde sayısı (1-50, varsayılan 25)" }
          },
          "required": ["mevzuat_id", "keyword"]
        }
        """);

    public ToolSpec Spec => new("search_within_mevzuat",
        "Tek bir mevzuatın (kanun, KHK, yönetmelik...) metni içinde madde bazında arama yapar; eşleşen maddeleri tam metinleriyle, eşleşme sayısına göre sıralı döner. Büyük kanunlarda fetch_document yerine bunu kullanın.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var mevzuatId = ToolArgs.String(arguments, "mevzuat_id");
        var keyword = ToolArgs.String(arguments, "keyword");
        if (string.IsNullOrWhiteSpace(mevzuatId) || string.IsNullOrWhiteSpace(keyword))
        {
            return ToolResult.Error("'mevzuat_id' ve 'keyword' gereklidir.");
        }
        var caseSensitive = ToolArgs.Bool(arguments, "case_sensitive");
        var maxResults = Math.Clamp(ToolArgs.Int(arguments, "max_results") ?? 25, 1, 50);

        try
        {
            var response = await search.SearchWithinMevzuatAsync(mevzuatId.Trim(), keyword, caseSensitive, maxResults, ct);
            var results = response.Results.Select(m => new
            {
                madde_no = m.MaddeNo,
                match_count = m.MatchCount,
                preview = m.Preview,
                content = m.Content,
            });
            return ToolResult.Ok(ToolArgs.Result(new
            {
                mevzuat_id = response.MevzuatId,
                keyword = response.Keyword,
                total_articles = response.TotalArticles,
                matching_articles = response.MatchingArticles,
                results,
            }));
        }
        catch (Exception ex) when (ex is BadRequestException or NotFoundException)
        {
            return ToolResult.Error(ex.Message);
        }
    }
}
