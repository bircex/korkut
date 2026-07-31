using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Search inside one piece of legislation at article level: downloads the full text (by mevzuatId
/// from a <c>search_decisions</c> source=mevzuat result), splits it into MADDE blocks and returns
/// the articles matching a boolean keyword query, ranked by match count with full article text.
/// The right tool for large laws (TCK, TTK...) where <c>fetch_document</c> would return far too
/// much. Ported from the Python <c>search_within_mevzuat</c>.
/// </summary>
public sealed class SearchWithinMevzuatTool(MevzuatClient client) : IAgentTool
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

        string? text;
        try
        {
            text = await client.GetDocumentTextAsync(mevzuatId.Trim(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        {
            return ToolResult.Error($"Mevzuat metni getirilemedi: {ex.Message}");
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return ToolResult.Error($"'{mevzuatId}' için mevzuat metni bulunamadı.");
        }

        var articles = MaddeSearch.Split(text);
        if (articles.Count == 0)
        {
            return ToolResult.Error(
                $"'{mevzuatId}' metni maddelere ayrılamadı (MADDE başlığı bulunamadı); tam metin için fetch_document kullanın.");
        }

        var matches = new List<(MaddeSearch.Madde Madde, int Score)>();
        foreach (var madde in articles)
        {
            var (isMatch, score) = MaddeSearch.Matches(madde.Content, keyword, caseSensitive);
            if (isMatch && score > 0)
            {
                matches.Add((madde, score));
            }
        }

        var ranked = matches
            .OrderByDescending(m => m.Score)
            .Take(maxResults)
            .Select(m => new
            {
                madde_no = m.Madde.No,
                match_count = m.Score,
                preview = MaddeSearch.Preview(m.Madde.Content, keyword, caseSensitive),
                content = m.Madde.Content,
            })
            .ToList();

        return ToolResult.Ok(ToolArgs.Result(new
        {
            mevzuat_id = mevzuatId,
            keyword,
            total_articles = articles.Count,
            matching_articles = matches.Count,
            results = ranked,
        }));
    }
}
