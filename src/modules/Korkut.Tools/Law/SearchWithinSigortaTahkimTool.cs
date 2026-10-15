using Korkut.Contract.LawClients;
using Korkut.Core;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Search for a keyword within one Sigorta Tahkim journal issue: splits it into individual
/// arbitration decisions and returns those mentioning the keyword, ranked by match count with a
/// short excerpt. Thin adapter over <see cref="ILawClientsSearchService"/>.
/// </summary>
public sealed class SearchWithinSigortaTahkimTool(ILawClientsSearchService search) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "issue_number": { "type": "string", "description": "Dergi sayı numarası (sigorta_tahkim arama sonucundan)" },
            "keyword": { "type": "string", "description": "Sayı içinde aranacak kelime/ifade (TR, büyük/küçük harf duyarsız)" },
            "max_results": { "type": "integer", "description": "En fazla eşleşen karar sayısı (varsayılan 10)" }
          },
          "required": ["issue_number", "keyword"]
        }
        """);

    public ToolSpec Spec => new("search_within_sigorta_tahkim",
        "Tek bir Sigorta Tahkim karar dergisi sayısı içinde anahtar kelime arar ve eşleşen hakem kararlarını döner.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.ResearchUse;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var issueNumber = ToolArgs.String(arguments, "issue_number");
        var keyword = ToolArgs.String(arguments, "keyword");
        if (string.IsNullOrWhiteSpace(issueNumber) || string.IsNullOrWhiteSpace(keyword))
        {
            return ToolResult.Error("'issue_number' ve 'keyword' gereklidir.");
        }
        var maxResults = Math.Max(1, ToolArgs.Int(arguments, "max_results") ?? 10);

        try
        {
            var response = await search.SearchWithinSigortaTahkimAsync(issueNumber, keyword, maxResults, ct);
            return ToolResult.Ok(ToolArgs.Result(new
            {
                issue_number = response.IssueNumber,
                keyword = response.Keyword,
                total_decisions = response.TotalDecisions,
                matching_decisions = response.MatchingDecisions,
                results = response.Results,
            }));
        }
        catch (Exception ex) when (ex is BadRequestException or NotFoundException)
        {
            return ToolResult.Error(ex.Message);
        }
    }
}
