using System.Runtime.CompilerServices;
using System.Text.Json;
using Korkut.Agent.Brain.Services;
using Korkut.Contract.Agent.Model;
using Korkut.Tools.Core;
using SourceRef = Korkut.Contract.Agent.Chat.SourceRef;

namespace Korkut.Agent.Brain.Unit.Tests;

/// <summary>
/// The tool-calling loop: it runs the tools the model asks for, feeds the results back, repeats
/// until a final answer, sums the token usage across every model call, and accumulates the legal
/// sources tool results surface. Driven by fakes (no LLM, no DB), so the orchestration is pinned
/// directly. The loop consumes the model via <see cref="IChatModelService.StreamAsync"/>, so the
/// scripted fake replays each reply as a stream (deltas, then tool calls, then usage).
/// </summary>
public class ToolCallingLoopTests
{
    private static readonly JsonElement NoArgs = ToolSchema.Parse("{}");

    private static AgentToolContext Context() => new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    /// <summary>Returns a scripted sequence of replies, one per streamed model turn.</summary>
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
            Calls++;
            var reply = _i < replies.Length ? replies[_i++] : replies[^1];
            if (reply.Text.Length > 0)
            {
                yield return ChatStreamEvent.Text(reply.Text);
            }

            if (reply.ToolCalls is { Count: > 0 } calls)
            {
                yield return ChatStreamEvent.Tools(calls);
            }

            yield return ChatStreamEvent.Final(reply.Usage);
        }
    }

    private sealed class FakeCatalog(params string[] toolNames) : IToolCatalog
    {
        /// <summary>Optional per-tool sources attached to the result (as a legal search tool would).</summary>
        public Dictionary<string, IReadOnlyList<SourceRef>> Sources { get; } = [];

        public List<string> Invoked { get; } = [];

        public IReadOnlyList<ToolSpec> AvailableSpecs(AgentToolContext context)
            => toolNames.Select(n => new ToolSpec(n, n, NoArgs)).ToList();

        public Task<ToolResult> InvokeAsync(string name, JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
        {
            Invoked.Add(name);
            return Task.FromResult(ToolResult.Ok($"{name} sonucu", Sources.GetValueOrDefault(name)));
        }
    }

    private static SourceRef Karar(string id) =>
        new("ictihat", "yargitay", id, $"Yargıtay {id}", Date: "2020-01-01");

    [Fact]
    public async Task A_final_answer_with_no_tool_calls_returns_immediately()
    {
        var model = new ScriptedModel(new ChatReply("Merhaba", new ChatUsage("fake", "m", 10, 5)));
        var loop = new ToolCallingLoopService(model, new FakeCatalog("create_matter"));

        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "selam")], Context(), ModelSpecialty.Dispatcher);

        Assert.Equal("Merhaba", reply.Text);
        Assert.Equal(1, model.Calls);          // no tool round-trip
        Assert.Equal(10, reply.Usage.PromptTokens);
        Assert.Null(reply.Sources);            // no tool ran, no sources
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

    [Fact]
    public async Task Tool_sources_accumulate_across_calls_and_iterations_in_call_order()
    {
        // Two searches over two iterations; each attaches sources to its result.
        var turn1 = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls:
            [
                new ToolInvocation("c1", "search_decisions", NoArgs),
                new ToolInvocation("c2", "search_office_documents", NoArgs),
            ]);
        var turn2 = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls: [new ToolInvocation("c3", "search_decisions", NoArgs)]);
        var final = new ChatReply("Cevap.", new ChatUsage("fake", "m", 1, 1));
        var catalog = new FakeCatalog("search_decisions", "search_office_documents");
        catalog.Sources["search_decisions"] = [Karar("2015/89"), Karar("2013/24590")];
        var loop = new ToolCallingLoopService(new ScriptedModel(turn1, turn2, final), catalog);

        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "emsal bul")], Context(), ModelSpecialty.Dispatcher);

        Assert.Equal("Cevap.", reply.Text);
        // Both search_decisions calls contributed (duplicates preserved — dedup is the chat service's job).
        Assert.NotNull(reply.Sources);
        Assert.Equal(4, reply.Sources!.Count);
        Assert.All(reply.Sources, s => Assert.Equal("ictihat", s.Kind));
    }

    [Fact]
    public async Task OnTool_receives_each_raw_tool_name_as_it_runs()
    {
        var callTurn = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls:
            [
                new ToolInvocation("c1", "search_decisions", NoArgs),
                new ToolInvocation("c2", "create_matter", NoArgs),
            ]);
        var final = new ChatReply("Bitti.", new ChatUsage("fake", "m", 1, 1));
        var loop = new ToolCallingLoopService(new ScriptedModel(callTurn, final), new FakeCatalog("search_decisions", "create_matter"));

        var toolNames = new List<string>();
        await loop.RunAsync("sys", [new ChatMessage("user", "soru")], Context(),
            ModelSpecialty.Dispatcher, onTool: n => { toolNames.Add(n); return Task.CompletedTask; });

        Assert.Equal(["search_decisions", "create_matter"], toolNames);
    }

    [Fact]
    public async Task Streamed_deltas_reach_onToken_as_they_arrive()
    {
        var model = new ScriptedModel(new ChatReply("Merhaba", new ChatUsage("fake", "m", 1, 1)));
        var loop = new ToolCallingLoopService(model, new FakeCatalog());

        var tokens = new List<string>();
        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "selam")], Context(),
            ModelSpecialty.Dispatcher, onToken: t => { tokens.Add(t); return Task.CompletedTask; });

        Assert.Equal("Merhaba", reply.Text);
        Assert.Equal(["Merhaba"], tokens);
    }
}
