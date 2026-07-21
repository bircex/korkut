using System.Text.Json;
using Korkut.Agent.Tools;
using Korkut.Agent.Tools.Law;
using Korkut.Application.Runtime;
using Korkut.Application.Security;

namespace Korkut.Agent.Tests;

/// <summary>
/// The composite legal tools' dispatch layer: routing by <c>source</c> to the matching
/// <see cref="ISourceClient"/>, unknown-source and unsupported-fetch handling, API-key gating of a
/// keyed source, and the read-permission RBAC. The concrete portal clients (Bedesten, …) hit live
/// government hosts and are exercised separately; here everything runs over fakes.
/// </summary>
public class LawToolsTests
{
    private static ToolContext Ctx(params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), permissions.ToHashSet());

    private static readonly ToolContext Reader = new(Guid.NewGuid(), Guid.NewGuid(),
        new HashSet<string> { Permissions.DocumentsRead });

    private static JsonElement Args(object o) => JsonSerializer.SerializeToElement(o);

    private sealed class FakeEnv(Dictionary<string, string> vars) : IEnvironment
    {
        public string? Get(string name) => vars.GetValueOrDefault(name);
    }

    private sealed class FakeSource(
        string source, bool supportsFetch = true, string? requiresKey = null) : ISourceClient
    {
        public string Source => source;
        public string? RequiresKey => requiresKey;
        public bool SupportsFetch => supportsFetch;

        public Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SourceResult>>(
                [new SourceResult(source, "doc-1", $"hit for {query}", CaseNumber: "2024/1")]);

        public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
            => Task.FromResult<string?>($"full text of {id}");
    }

    [Fact]
    public async Task Search_dispatches_to_the_matching_source()
    {
        var tool = new SearchDecisionsTool([new FakeSource("yargitay")], new FakeEnv([]));
        var result = await tool.InvokeAsync(Args(new { source = "yargitay", query = "kira" }), Reader);

        Assert.False(result.IsError);
        Assert.Contains("yargitay", result.Content);
        Assert.Contains("hit for kira", result.Content);
        Assert.Contains("2024/1", result.Content); // snake_case case_number is serialized
    }

    [Fact]
    public async Task Search_on_an_unknown_source_errors()
    {
        var tool = new SearchDecisionsTool([new FakeSource("yargitay")], new FakeEnv([]));
        var result = await tool.InvokeAsync(Args(new { source = "atlantis", query = "x" }), Reader);

        Assert.True(result.IsError);
        Assert.Contains("Desteklenmeyen", result.Content);
    }

    [Fact]
    public async Task Search_on_a_keyed_source_without_its_key_errors()
    {
        var tool = new SearchDecisionsTool(
            [new FakeSource("bddk", requiresKey: "TAVILY_API_KEY")], new FakeEnv([]));
        var result = await tool.InvokeAsync(Args(new { source = "bddk", query = "x" }), Reader);

        Assert.True(result.IsError);
        Assert.Contains("API anahtarı", result.Content);
    }

    [Fact]
    public async Task Search_on_a_keyed_source_with_its_key_works()
    {
        var env = new FakeEnv(new Dictionary<string, string> { ["TAVILY_API_KEY"] = "tvly-1" });
        var tool = new SearchDecisionsTool([new FakeSource("bddk", requiresKey: "TAVILY_API_KEY")], env);
        var result = await tool.InvokeAsync(Args(new { source = "bddk", query = "x" }), Reader);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task Fetch_returns_full_text_from_the_matching_source()
    {
        var tool = new FetchDocumentTool([new FakeSource("yargitay")], new FakeEnv([]));
        var result = await tool.InvokeAsync(Args(new { source = "yargitay", id = "doc-1" }), Reader);

        Assert.False(result.IsError);
        Assert.Equal("full text of doc-1", result.Content);
    }

    [Fact]
    public async Task Fetch_on_a_source_that_does_not_support_it_errors()
    {
        var tool = new FetchDocumentTool([new FakeSource("mevzuat", supportsFetch: false)], new FakeEnv([]));
        var result = await tool.InvokeAsync(Args(new { source = "mevzuat", id = "1" }), Reader);

        Assert.True(result.IsError);
        Assert.Contains("desteklemiyor", result.Content);
    }

    [Fact]
    public void Legal_tools_require_the_documents_read_permission()
    {
        var search = new SearchDecisionsTool([], new FakeEnv([]));
        var fetch = new FetchDocumentTool([], new FakeEnv([]));

        Assert.Equal(Permissions.DocumentsRead, search.RequiredPermission);
        Assert.Equal(Permissions.DocumentsRead, fetch.RequiredPermission);
    }
}
