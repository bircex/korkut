using Korkut.Agent.Brain;
using Korkut.Agent.Model;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// The tool-calling loop: it runs the tools the model asks for, feeds the results back, repeats
/// until a final answer, and sums the token usage across every model call. Driven by fakes (no LLM,
/// no DB), so the orchestration is pinned directly.
/// </summary>
public class ToolCallingLoopTests
{
    private static readonly JsonElement NoArgs = ToolSchema.Parse("{}");

    private static AgentToolContext Context() => new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    /// <summary>Returns a scripted sequence of replies, one per CompleteAsync call.</summary>
    private sealed class ScriptedModel(params ChatReply[] replies) : IChatModelService
    {
        private int _i;

        public int Calls { get; private set; }

        public Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(_i < replies.Length ? replies[_i++] : replies[^1]);
        }

        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
            ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakeCatalog(params string[] toolNames) : IToolCatalog
    {
        public List<string> Invoked { get; } = [];

        public IReadOnlyList<ToolSpec> AvailableSpecs(AgentToolContext context)
            => toolNames.Select(n => new ToolSpec(n, n, NoArgs)).ToList();

        public Task<ToolResult> InvokeAsync(string name, JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
        {
            Invoked.Add(name);
            return Task.FromResult(ToolResult.Ok($"{name} sonucu"));
        }
    }

    [Fact]
    public async Task A_final_answer_with_no_tool_calls_returns_immediately()
    {
        var model = new ScriptedModel(new ChatReply("Merhaba", new ChatUsage("fake", "m", 10, 5)));
        var loop = new ToolCallingLoopService(model, new FakeCatalog("create_matter"));

        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "selam")], Context(), ModelSpecialty.Dispatcher);

        Assert.Equal("Merhaba", reply.Text);
        Assert.Equal(1, model.Calls);          // no tool round-trip
        Assert.Equal(10, reply.Usage.PromptTokens);
    }

    [Fact]
    public async Task A_tool_call_is_run_and_the_result_is_fed_back_before_the_final_answer()
    {
        var callTurn = new ChatReply(
            "", new ChatUsage("fake", "m", 8, 2),
            ToolCalls: [new ToolInvocation("call-1", "create_matter", NoArgs)]);
        var finalTurn = new ChatReply("Dosya açıldı.", new ChatUsage("fake", "m", 12, 4));
        var model = new ScriptedModel(callTurn, finalTurn);
        var catalog = new FakeCatalog("create_matter");
        var loop = new ToolCallingLoopService(model, catalog);

        var steps = new List<string>();
        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "dosya aç")], Context(),
            ModelSpecialty.Dispatcher, onStep: s => { steps.Add(s); return Task.CompletedTask; });

        Assert.Equal("Dosya açıldı.", reply.Text);
        Assert.Equal(["create_matter"], catalog.Invoked);        // the tool ran
        Assert.Equal(2, model.Calls);                            // call turn + final turn
        Assert.Equal(20, reply.Usage.PromptTokens);              // 8 + 12 summed
        Assert.Equal(6, reply.Usage.CompletionTokens);           // 2 + 4 summed
        Assert.Contains(steps, s => s.Contains("create_matter"));
    }
}
