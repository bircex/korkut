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
        new("ictihat", "yargitay", id, $"Yargıtay {id}", Date: "2020-01-01", CaseNumber: id);

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
        // Steps carry the human activity label, never the raw tool name.
        Assert.Contains("Dosya açıyorum", steps);
    }

    [Fact]
    public async Task Search_and_fetch_steps_name_the_legal_database_from_the_source_argument()
    {
        var args = ToolSchema.Parse("""{"source":"yargitay","query":"hakaret"}""");
        var callTurn = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls:
            [
                new ToolInvocation("c1", "search_decisions", args),
                new ToolInvocation("c2", "fetch_document", args),
            ]);
        var final = new ChatReply("Bitti.", new ChatUsage("fake", "m", 1, 1));
        var loop = new ToolCallingLoopService(new ScriptedModel(callTurn, final), new FakeCatalog("search_decisions", "fetch_document"));

        var steps = new List<string>();
        await loop.RunAsync("sys", [new ChatMessage("user", "soru")], Context(),
            ModelSpecialty.Dispatcher, onStep: s => { steps.Add(s); return Task.CompletedTask; });

        Assert.Contains("Yargıtay kararlarında arıyorum", steps);
        Assert.Contains("Yargıtay kararının tam metnini okuyorum", steps);
    }

    [Fact]
    public async Task Only_the_sources_the_final_answer_cites_survive()
    {
        // Two searches over two iterations; each attaches sources to its result. The final answer
        // cites only 2015/89 — the uncited 2013/24590 candidates are dropped as noise.
        var turn1 = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls:
            [
                new ToolInvocation("c1", "search_decisions", NoArgs),
                new ToolInvocation("c2", "search_office_documents", NoArgs),
            ]);
        var turn2 = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls: [new ToolInvocation("c3", "search_decisions", NoArgs)]);
        var final = new ChatReply("Yargıtay 2015/89 E. sayılı kararına göre suçtur.", new ChatUsage("fake", "m", 1, 1));
        var catalog = new FakeCatalog("search_decisions", "search_office_documents");
        catalog.Sources["search_decisions"] = [Karar("2015/89"), Karar("2013/24590")];
        var loop = new ToolCallingLoopService(new ScriptedModel(turn1, turn2, final), catalog);

        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "emsal bul")], Context(), ModelSpecialty.Dispatcher);

        // The cited decision came back from BOTH search calls (duplicates preserved — dedup is the
        // chat service's job); the uncited one is gone entirely.
        Assert.NotNull(reply.Sources);
        Assert.Equal(2, reply.Sources!.Count);
        Assert.All(reply.Sources, s => Assert.Equal("2015/89", s.CaseNumber));
    }

    [Fact]
    public async Task An_answer_that_cites_nothing_carries_no_sources()
    {
        var callTurn = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls: [new ToolInvocation("c1", "search_decisions", NoArgs)]);
        var final = new ChatReply("Bu konuda emsal karar bulamadım.", new ChatUsage("fake", "m", 1, 1));
        var catalog = new FakeCatalog("search_decisions");
        catalog.Sources["search_decisions"] = [Karar("2015/89"), Karar("2013/24590")];
        var loop = new ToolCallingLoopService(new ScriptedModel(callTurn, final), catalog);

        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "emsal bul")], Context(), ModelSpecialty.Dispatcher);

        Assert.Null(reply.Sources);
    }

    [Fact]
    public async Task A_cited_law_number_keeps_the_legislation_source()
    {
        var callTurn = new ChatReply("", new ChatUsage("fake", "m", 1, 1),
            ToolCalls: [new ToolInvocation("c1", "search_decisions", NoArgs)]);
        var final = new ChatReply("5816 sayılı Kanun m.1 uyarınca cezalandırılır.", new ChatUsage("fake", "m", 1, 1));
        var catalog = new FakeCatalog("search_decisions");
        catalog.Sources["search_decisions"] =
        [
            new SourceRef("mevzuat", "mevzuat", "5816", "Atatürk Aleyhine İşlenen Suçlar Hakkında Kanun"),
            new SourceRef("mevzuat", "mevzuat", "6098", "Türk Borçlar Kanunu"),
        ];
        var loop = new ToolCallingLoopService(new ScriptedModel(callTurn, final), catalog);

        var reply = await loop.RunAsync("sys", [new ChatMessage("user", "hangi kanun?")], Context(), ModelSpecialty.Dispatcher);

        var only = Assert.Single(reply.Sources!);
        Assert.Equal("5816", only.Id);
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
