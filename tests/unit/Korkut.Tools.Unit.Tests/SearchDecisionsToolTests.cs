using System.Text.Json;
using Korkut.Core;
using Korkut.Tools.Core;
using Korkut.Tools.Law;

namespace Korkut.Tools.Unit.Tests;

/// <summary>
/// search_decisions surfaces its hits twice off one search: the JSON text the model reads, and the
/// structured <c>SourceRef</c> list the UI's sources drawer consumes (kind = mevzuat for the
/// legislation source, ictihat for every court source).
/// </summary>
public class SearchDecisionsToolTests
{
    private sealed class FakeClient(string source, params SourceResult[] results) : ISourceClient
    {
        public string Source => source;

        public string? RequiresKey => null;

        public bool SupportsFetch => false;

        public Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SourceResult>>(results);

        public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeEnv : IEnvironment
    {
        public string? Get(string key) => null;
    }

    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string source) =>
        JsonDocument.Parse($"{{\"source\":\"{source}\",\"query\":\"hakaret\"}}").RootElement;

    [Fact]
    public async Task Court_hits_are_attached_as_ictihat_sources_with_all_metadata()
    {
        var hit = new SourceResult(
            "yargitay", "id-1", "CGK 2015/89 E. 2018/140 K.", "2018-04-02", "2015/89", "2018/140",
            "Yargıtay", "Ceza Genel Kurulu", "https://example.org/k", "kaypak adamsın…");
        var tool = new SearchDecisionsTool([new FakeClient("yargitay", hit)], new FakeEnv());

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
        var hit = new SourceResult("mevzuat", "5237", "Türk Ceza Kanunu");
        var tool = new SearchDecisionsTool([new FakeClient("mevzuat", hit)], new FakeEnv());

        var result = await tool.InvokeAsync(Args("mevzuat"), Ctx);

        var source = Assert.Single(result.Sources!);
        Assert.Equal("mevzuat", source.Kind);
    }

    [Fact]
    public async Task An_unsupported_source_errors_without_sources()
    {
        var tool = new SearchDecisionsTool([new FakeClient("yargitay")], new FakeEnv());

        var result = await tool.InvokeAsync(Args("bilinmeyen"), Ctx);

        Assert.True(result.IsError);
        Assert.Null(result.Sources);
    }
}
