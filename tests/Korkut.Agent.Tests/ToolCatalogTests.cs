using System.Text.Json;
using Korkut.Agent.Tools;
using Korkut.Application.Abstractions.Knowledge;
using Korkut.Application.Runtime;
using Korkut.Application.Security;
using Korkut.Domain.Knowledge;

namespace Korkut.Agent.Tests;

/// <summary>
/// The agent-tool abstraction: key-gated availability (a tool needing an unset API key is hidden),
/// name dispatch, unknown-tool handling, and the KnowledgeSearch tool's formatting — all pure unit
/// tests over fakes (no host/container).
/// </summary>
public class ToolCatalogTests
{
    private static ToolContext Ctx(params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), permissions.ToHashSet());

    private static readonly JsonElement NoArgs = JsonSerializer.SerializeToElement(new { });

    private sealed class FakeEnv(Dictionary<string, string> vars) : IEnvironment
    {
        public string? Get(string name) => vars.GetValueOrDefault(name);
    }

    private sealed class FakeTool(string name, string? requiresKey, string? requiredPermission = null) : IAgentTool
    {
        public ToolSpec Spec => new(name, "fake", JsonSerializer.SerializeToElement(new { type = "object" }));
        public string? RequiresKey => requiresKey;
        public string? RequiredPermission => requiredPermission;
        public Task<ToolResult> InvokeAsync(JsonElement a, ToolContext c, CancellationToken ct = default)
            => Task.FromResult(ToolResult.Ok($"ran:{name}"));
    }

    [Fact]
    public void Key_gated_tool_is_hidden_when_its_key_is_absent()
    {
        var catalog = new ToolCatalog([new FakeTool("free", null), new FakeTool("paid", "PAID_KEY")], new FakeEnv([]));
        var names = catalog.AvailableSpecs(Ctx()).Select(s => s.Name).ToList();

        Assert.Contains("free", names);
        Assert.DoesNotContain("paid", names);
    }

    [Fact]
    public void Key_gated_tool_is_available_when_its_key_is_present()
    {
        var env = new FakeEnv(new Dictionary<string, string> { ["PAID_KEY"] = "sk-123" });
        var catalog = new ToolCatalog([new FakeTool("paid", "PAID_KEY")], env);
        Assert.Contains("paid", catalog.AvailableSpecs(Ctx()).Select(s => s.Name));
    }

    [Fact]
    public async Task Available_tool_is_dispatched_by_name()
    {
        var catalog = new ToolCatalog([new FakeTool("free", null)], new FakeEnv([]));
        var result = await catalog.InvokeAsync("free", NoArgs, Ctx());
        Assert.False(result.IsError);
        Assert.Equal("ran:free", result.Content);
    }

    [Fact]
    public async Task Unknown_or_gated_tool_returns_an_error_result()
    {
        var catalog = new ToolCatalog([new FakeTool("paid", "PAID_KEY")], new FakeEnv([]));
        var unknown = await catalog.InvokeAsync("nope", NoArgs, Ctx());
        var gated = await catalog.InvokeAsync("paid", NoArgs, Ctx());

        Assert.True(unknown.IsError);
        Assert.True(gated.IsError); // hidden because its key is unset
    }

    private sealed class FakeKnowledge(params string[] texts) : IKnowledgeSearch
    {
        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SearchHit>>(
                texts.Select((t, i) => new SearchHit(Guid.NewGuid(), Guid.NewGuid(), i, t, 0.9)).ToList());
    }

    [Fact]
    public async Task KnowledgeSearchTool_returns_the_matched_passages()
    {
        var tool = new KnowledgeSearchTool(new FakeKnowledge("Kira feshi ihtarnamesi", "Tahliye süreci"));
        var args = JsonSerializer.SerializeToElement(new { query = "kira" });

        var result = await tool.InvokeAsync(args, Ctx());
        Assert.False(result.IsError);
        Assert.Contains("Kira feshi ihtarnamesi", result.Content);
        Assert.Contains("Tahliye süreci", result.Content);
    }

    [Fact]
    public async Task KnowledgeSearchTool_requires_a_query()
    {
        var tool = new KnowledgeSearchTool(new FakeKnowledge("x"));
        var result = await tool.InvokeAsync(NoArgs, Ctx());
        Assert.True(result.IsError);
    }

    [Fact]
    public void A_tool_the_caller_lacks_permission_for_is_hidden_from_the_model()
    {
        var catalog = new ToolCatalog([new FakeTool("write_matter", null, Permissions.MattersWrite)], new FakeEnv([]));
        Assert.Contains("write_matter", catalog.AvailableSpecs(Ctx(Permissions.MattersWrite)).Select(s => s.Name));
        Assert.DoesNotContain("write_matter", catalog.AvailableSpecs(Ctx()).Select(s => s.Name)); // no permission
    }

    [Fact]
    public async Task Invoking_a_tool_without_permission_is_refused()
    {
        var catalog = new ToolCatalog([new FakeTool("write_matter", null, Permissions.MattersWrite)], new FakeEnv([]));
        var refused = await catalog.InvokeAsync("write_matter", NoArgs, Ctx()); // caller lacks matters.write
        Assert.True(refused.IsError);
        Assert.Contains("yetkiniz yok", refused.Content);
    }
}
