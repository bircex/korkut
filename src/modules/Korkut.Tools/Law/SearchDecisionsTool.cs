using Korkut.Contract.LawClients;
using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Composite legal-search tool: searches a chosen public Turkish legal database. Thin adapter over
/// <see cref="ILawClientsSearchService"/> (same pattern as <c>Korkut.Tools/Office/FinanceSummaryTool.cs</c>
/// over <c>IFinanceService</c>) — the dispatch-by-source logic lives in the LawClients module now,
/// shared with the Karar Arama UI's search endpoint.
/// </summary>
public sealed class SearchDecisionsTool(ILawClientsSearchService search) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "source": { "type": "string", "enum": ["yargitay","danistay","emsal","kyb","anayasa","mevzuat","gib","kik","rekabet","sayistay","uyusmazlik","bddk","kvkk","sigorta_tahkim"], "description": "Aranacak hukuki veritabanı" },
            "query": { "type": "string", "description": "Arama sorgusu. Çok kelimeli sorguda kelimelerden HERHANGİ biri eşleşir (sonuç şişer); tam ifade için tırnak kullan: \"silahlı terör örgütü\". Kısa ve odaklı anahtar kelimeler en iyi sonucu verir" },
            "limit": { "type": "integer", "description": "Sonuç sayısı (varsayılan 10)" },
            "page": { "type": "integer", "description": "Sayfa (varsayılan 1)" },
            "sort": { "type": "string", "enum": ["relevance", "date"], "description": "Sıralama (yargitay/danistay/emsal/kyb): relevance = alaka (varsayılan, önerilen), date = en yeni karar" },
            "date_start": { "type": "string", "description": "YYYY-MM-DD; karar tarihi (yargitay/danistay/emsal/kyb/kik) veya Resmî Gazete tarihi (mevzuat)" },
            "date_end": { "type": "string", "description": "YYYY-MM-DD" },
            "chamber": { "type": "string", "description": "Daire (yargitay/danistay)" },
            "decision_type": { "type": "string", "description": "rekabet/kik/sayistay için karar türü (ör. sayistay: genel_kurul/temyiz_kurulu/daire; kik: uyusmazlik/duzenleyici/mahkeme)" },
            "application_type": { "type": "string", "description": "anayasa için: norm veya individual" },
            "scope": { "type": "string", "description": "uyusmazlik için arama kapsamı: all/esas/karar" },
            "legislation_type": { "type": "string", "description": "mevzuat için tür filtresi, virgülle çoklu: KANUN, CB_KARARNAME, YONETMELIK, CB_YONETMELIK, CB_KARAR, CB_GENELGE, KHK, TUZUK, KKY (kurum yönetmelikleri), UY (üniversite yönetmelikleri), TEBLIGLER, MULGA" },
            "search_scope": { "type": "string", "enum": ["title", "content", "title_and_content"], "description": "mevzuat için arama kapsamı. title: yalnızca mevzuat adında (varsayılan). content: metin içinde tam metin arama; Solr sözdizimi desteklenir (\"tam ifade\", +zorunlu -hariç, joker*, bulanık~). title_and_content: hem ad hem metin" },
            "tam_cumle": { "type": "boolean", "description": "mevzuat için: sorguyu tam ifade olarak eşleştir (varsayılan false, kelime bazlı)" },
            "mevzuat_no": { "type": "string", "description": "mevzuat için numara filtresi (ör. 5237 Türk Ceza Kanunu, 6102 Türk Ticaret Kanunu). Yalnızca rakamdan oluşan bir query otomatik bu filtreye çevrilir" },
            "gazette_number": { "type": "string", "description": "mevzuat için Resmî Gazete sayısı filtresi" }
          },
          "required": ["source", "query"]
        }
        """);

    public ToolSpec Spec => new("search_decisions",
        "Türk hukuki veritabanlarında (Yargıtay, Danıştay, Emsal, Mevzuat, Anayasa, GİB, KİK...) karar/mevzuat araması yapar.",
        Schema);

    public string? RequiresKey => null; // always offered; a source whose key is missing errors at runtime

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var source = ToolArgs.String(arguments, "source");
        var query = ToolArgs.String(arguments, "query");
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Error("'source' ve 'query' gereklidir.");
        }

        var request = new LawClientsSearchRequest(
            query.Trim(),
            ToolArgs.Int(arguments, "limit"),
            ToolArgs.Int(arguments, "page"),
            ToolArgs.String(arguments, "sort"),
            ToolArgs.String(arguments, "date_start"),
            ToolArgs.String(arguments, "date_end"),
            ToolArgs.String(arguments, "chamber"),
            ToolArgs.String(arguments, "decision_type"),
            ToolArgs.String(arguments, "application_type"),
            ToolArgs.String(arguments, "scope"),
            ToolArgs.String(arguments, "legislation_type"),
            ToolArgs.String(arguments, "search_scope"),
            ToolArgs.Bool(arguments, "tam_cumle"),
            ToolArgs.String(arguments, "mevzuat_no"),
            ToolArgs.String(arguments, "gazette_number"));

        try
        {
            var response = await search.SearchAsync(source, request, ct);
            // The model reads the JSON text; the SourceRef list rides along for the UI's sources
            // drawer (kind splits legislation from case law — "mevzuat" is the only statute source).
            var kind = source.Equals("mevzuat", StringComparison.OrdinalIgnoreCase) ? "mevzuat" : "ictihat";
            var sources = response.Results
                .Select(r => new Korkut.Contract.Agent.Chat.SourceRef(
                    kind, r.Source, r.Id, r.Title, r.Date, r.CaseNumber, r.DecisionNumber,
                    r.Court, r.Chamber, r.Url, r.Snippet))
                .ToList();
            // total = the database's full match count (null when the source doesn't report one);
            // count = the hits actually pulled. The persona's coverage-transparency rule reads these.
            return ToolResult.Ok(
                ToolArgs.Result(new { total = response.Total, count = response.Count, results = response.Results }), sources);
        }
        catch (Exception ex) when (ex is BadRequestException or NotFoundException)
        {
            return ToolResult.Error(ex.Message);
        }
    }
}
