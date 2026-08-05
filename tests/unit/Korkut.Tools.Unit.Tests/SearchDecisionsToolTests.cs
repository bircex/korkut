using System.Text.Json;
using Korkut.Contract.LawClients;
using Korkut.Core;
using Korkut.Tools.Core;
using Korkut.Tools.Law;

namespace Korkut.Tools.Unit.Tests;

/// <summary>
/// search_decisions surfaces its hits twice off one search: the JSON text the model reads, and the
/// structured <c>SourceRef</c> list the UI's sources drawer consumes (kind = mevzuat for the
/// legislation source, ictihat for every court source). The dispatch-by-source logic itself now
/// lives in ILawClientsSearchService (Korkut.LawClients); this only pins the tool's own mapping.
/// </summary>
public class SearchDecisionsToolTests
{
    private sealed class FakeSearchService(string knownSource, params LawClientsSearchResult[] results) : ILawClientsSearchService
    {
        public Task<LawClientsSearchResponse> SearchAsync(string source, LawClientsSearchRequest request, CancellationToken ct = default)
        {
            if (!source.Equals(knownSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException($"Desteklenmeyen kaynak: {source}");
            }
            return Task.FromResult(new LawClientsSearchResponse(results.Length, results.Length, results));
        }

        public Task<LawClientsArticleSearchResponse> SearchWithinMevzuatAsync(
            string mevzuatId, string keyword, bool caseSensitive, int maxResults, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<LawClientsInsuranceSearchResponse> SearchWithinSigortaTahkimAsync(
            string issueNumber, string keyword, int maxResults, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string source) =>
        JsonDocument.Parse($"{{\"source\":\"{source}\",\"query\":\"hakaret\"}}").RootElement;

    [Fact]
    public async Task Court_hits_are_attached_as_ictihat_sources_with_all_metadata()
    {
        var hit = new LawClientsSearchResult(
            "yargitay", "id-1", "CGK 2015/89 E. 2018/140 K.", "2018-04-02", "2015/89", "2018/140",
            "Yargıtay", "Ceza Genel Kurulu", "https://example.org/k", "kaypak adamsın…");
        var tool = new SearchDecisionsTool(new FakeSearchService("yargitay", hit));

        var result = await tool.InvokeAsync(Args("yargitay"), Ctx);

        Assert.False(result.IsError);
        Assert.Contains("CGK 2015/89", result.Content); // model hâlâ JSON metnini görür
        var source = Assert.Single(result.Sources!);
        Assert.Equal("ictihat", source.Kind);
        Assert.Equal(("yargitay", "id-1", "2015/89", "2018/140"), (source.Source, source.Id, source.CaseNumber, source.DecisionNumber));
        Assert.Equal(("Yargıtay", "Ceza Genel Kurulu", "2018-04-02"), (source.Court, source.Chamber, source.Date));
        Assert.Equal("kaypak adamsın…", source.Snippet);
    }

    [Fact]
    public async Task Legislation_hits_are_attached_as_mevzuat_sources()
    {
        var hit = new LawClientsSearchResult("mevzuat", "5237", "Türk Ceza Kanunu", null, null, null, null, null, null, null);
        var tool = new SearchDecisionsTool(new FakeSearchService("mevzuat", hit));

        var result = await tool.InvokeAsync(Args("mevzuat"), Ctx);

        var source = Assert.Single(result.Sources!);
        Assert.Equal("mevzuat", source.Kind);
    }

    [Fact]
    public async Task An_unsupported_source_errors_without_sources()
    {
        var tool = new SearchDecisionsTool(new FakeSearchService("yargitay"));

        var result = await tool.InvokeAsync(Args("bilinmeyen"), Ctx);

        Assert.True(result.IsError);
        Assert.Null(result.Sources);
    }
}
