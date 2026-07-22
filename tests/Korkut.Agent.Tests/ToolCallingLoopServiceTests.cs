using System.Text.Json;
using Korkut.Agent.Tools;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Services.Core.Ai;
using Korkut.Agent.Services.Implementations.Ai;
using Korkut.Application.Ai;
using Korkut.Application.Runtime;

namespace Korkut.Agent.Tests;

/// <summary>
/// The tool-calling loop: it offers the catalog's permitted tools to the model, runs any tool the
/// model calls, feeds the result back, and repeats until a final text answer. Driven by a scripted
/// fake model + a real ToolCatalog with a fake tool — the loop logic is exercised end-to-end with
/// no real provider.
/// </summary>
public class ToolCallingLoopServiceTests
{
    private static readonly ToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private sealed class FakeEnv : IEnvironment
    {
        public string? Get(string name) => null;
    }

    private sealed class EchoTool : IAgentTool
    {
        public ToolSpec Spec => new("echo", "test aracı", JsonSerializer.SerializeToElement(new { type = "object" }));
        public string? RequiresKey => null;
        public string? RequiredPermission => null;
        public Task<ToolResult> InvokeAsync(JsonElement a, ToolContext c, CancellationToken ct = default)
            => Task.FromResult(ToolResult.Ok("TOOL_RAN"));
    }

    // First turn (no tool result yet, tools offered) → call the first tool; afterwards → final answer.
    private sealed class ScriptedModelService : IChatModelService
    {
        public Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        {
            var toolTurn = request.Messages.LastOrDefault(m => m.Role == "tool");
            if (toolTurn is null && request.Tools is { Count: > 0 } tools)
            {
                var call = new ToolInvocation("c1", tools[0].Name, JsonSerializer.SerializeToElement(new { }));
                return Task.FromResult(new ChatReply(string.Empty, ChatUsage.None, [call]));
            }

            var output = toolTurn?.ToolResults is { Count: > 0 } results ? results[0].Content : "none";
            return Task.FromResult(new ChatReply($"final:{output}", ChatUsage.None));
        }

        public IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task Runs_the_tool_the_model_calls_then_returns_the_final_answer()
    {
        var catalog = new ToolCatalog([new EchoTool()], new FakeEnv());
        var loop = new ToolCallingLoopService(new ScriptedModelService(), catalog);

        var answer = await loop.RunAsync("sistem", [new ChatMessage("user", "merhaba")], Ctx, ModelSpecialty.Dispatcher);

        // The model called echo, got "TOOL_RAN" back, then answered with it.
        Assert.Equal("final:TOOL_RAN", answer);
    }

    [Fact]
    public async Task Returns_directly_when_the_model_makes_no_tool_call()
    {
        var catalog = new ToolCatalog([], new FakeEnv()); // no tools offered
        var loop = new ToolCallingLoopService(new ScriptedModelService(), catalog);

        var answer = await loop.RunAsync("sistem", [new ChatMessage("user", "merhaba")], Ctx, ModelSpecialty.Dispatcher);
        Assert.Equal("final:none", answer);
    }
}
