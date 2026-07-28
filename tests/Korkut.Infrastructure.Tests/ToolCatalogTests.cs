using System.Text.Json;
using Korkut.Core;
using Korkut.Tools;
using Korkut.Tools.Core;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The tool catalog is the RBAC + configuration gate: a tool is offered and invocable only when
/// its key is configured AND the caller holds its permission. These are pure checks (no DB), so
/// they pin the gate directly.
/// </summary>
public class ToolCatalogTests
{
    private static readonly JsonElement NoArgs = ToolSchema.Parse("{}");

    private sealed class FakeEnv(params string[] present) : IEnvironment
    {
        private readonly HashSet<string> _present = [.. present];

        public string? Get(string name) => _present.Contains(name) ? "value" : null;
    }

    private sealed class FakeTool(string name, string? requiresKey, string? requiredPermission) : IAgentTool
    {
        public bool Invoked { get; private set; }

        public ToolSpec Spec => new(name, name, NoArgs);

        public string? RequiresKey => requiresKey;

        public string? RequiredPermission => requiredPermission;

        public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
        {
            Invoked = true;
            return Task.FromResult(ToolResult.Ok("done"));
        }
    }

    private static AgentToolContext Context(params string[] permissions)
        => new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>(permissions));

    [Fact]
    public void Advertises_only_configured_and_permitted_tools()
    {
        var free = new FakeTool("free", null, null);
        var needsKey = new FakeTool("needs_key", "TAVILY_API_KEY", null);
        var needsPerm = new FakeTool("needs_perm", null, "matters.write");
        var catalog = new ToolCatalog([free, needsKey, needsPerm], new FakeEnv()); // no key present

        var names = catalog.AvailableSpecs(Context("matters.read")).Select(s => s.Name).ToHashSet();

        Assert.Contains("free", names);
        Assert.DoesNotContain("needs_key", names);   // key not configured
        Assert.DoesNotContain("needs_perm", names);  // caller lacks matters.write
    }

    [Fact]
    public async Task Invoking_an_unpermitted_tool_is_refused_without_running_it()
    {
        var needsPerm = new FakeTool("danger", null, "matters.write");
        var catalog = new ToolCatalog([needsPerm], new FakeEnv());

        var result = await catalog.InvokeAsync("danger", NoArgs, Context("matters.read"));

        Assert.True(result.IsError);
        Assert.False(needsPerm.Invoked); // never executed — RBAC blocks before invocation
    }

    [Fact]
    public async Task A_configured_and_permitted_tool_runs()
    {
        var tool = new FakeTool("go", "TAVILY_API_KEY", "matters.write");
        var catalog = new ToolCatalog([tool], new FakeEnv("TAVILY_API_KEY"));

        var result = await catalog.InvokeAsync("go", NoArgs, Context("matters.write"));

        Assert.False(result.IsError);
        Assert.True(tool.Invoked);
    }

    [Fact]
    public async Task An_unknown_tool_name_is_refused()
    {
        var catalog = new ToolCatalog([], new FakeEnv());
        var result = await catalog.InvokeAsync("ghost", NoArgs, Context("matters.write"));
        Assert.True(result.IsError);
    }
}
