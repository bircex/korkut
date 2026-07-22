using System.Net;
using System.Text;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Agent.Ai;

namespace Korkut.Agent.Tests;

/// <summary>
/// The chat providers' tool-calling wire mapping: a tool definition and a prior tool-result turn are
/// serialized into each API's native shape (Anthropic tool_use/tool_result blocks; OpenAI
/// function tools + role="tool" messages), and a tool call in the response is parsed back into
/// <see cref="ChatReply.ToolCalls"/>. A stub handler captures the request and returns a canned reply,
/// so no network is touched.
/// </summary>
public class ProviderToolCallingTests
{
    private static readonly JsonElement Schema =
        JsonDocument.Parse("""{"type":"object","properties":{"query":{"type":"string"}}}""").RootElement;

    private sealed class FakeEnv(Dictionary<string, string?> vars) : IEnvironment
    {
        public string? Get(string name) => vars.TryGetValue(name, out var v) ? v : null;
    }

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static ChatRequest RequestWithToolTurn() => new(
        "sistem",
        [
            new ChatMessage("user", "yargıtay kararı bul"),
            new ChatMessage("assistant", "", ToolCalls: [new ToolInvocation("call-1", "search_decisions", Schema)]),
            new ChatMessage("tool", "", ToolResults: [new ToolOutput("call-1", "sonuç metni")]),
        ],
        ModelSpecialty.Dispatcher,
        Tools: [new ToolDefinition("search_decisions", "karar arar", Schema)]);

    [Fact]
    public async Task Anthropic_sends_tools_and_tool_blocks_and_parses_tool_use()
    {
        const string response = """
        {"content":[{"type":"text","text":"bir saniye"},
                    {"type":"tool_use","id":"tu_1","name":"search_decisions","input":{"source":"yargitay","query":"kira"}}],
         "usage":{"input_tokens":5,"output_tokens":7}}
        """;
        var handler = new StubHandler(response);
        var provider = new AnthropicChatProviderService(
            new HttpClient(handler), new FakeEnv(new() { ["ANTHROPIC_API_KEY"] = "sk-test" }));

        var reply = await provider.CompleteAsync(new ModelSelection("claude", "claude-x", 1024), RequestWithToolTurn());

        // Request: tools declared + the tool_use / tool_result blocks mapped.
        Assert.Contains("\"tools\"", handler.CapturedBody);
        Assert.Contains("\"input_schema\"", handler.CapturedBody);
        Assert.Contains("\"tool_use\"", handler.CapturedBody);
        Assert.Contains("\"tool_result\"", handler.CapturedBody);
        Assert.Contains("\"tool_use_id\":\"call-1\"", handler.CapturedBody);

        // Response: the tool_use block is parsed into a tool call.
        Assert.NotNull(reply.ToolCalls);
        var call = Assert.Single(reply.ToolCalls!);
        Assert.Equal("tu_1", call.Id);
        Assert.Equal("search_decisions", call.Name);
        Assert.Equal("yargitay", call.Arguments.GetProperty("source").GetString());
    }

    [Fact]
    public async Task OpenAi_sends_function_tools_and_tool_messages_and_parses_tool_calls()
    {
        const string response = """
        {"choices":[{"message":{"content":null,"tool_calls":[
            {"id":"call_9","type":"function","function":{"name":"search_decisions","arguments":"{\"source\":\"danistay\",\"query\":\"vergi\"}"}}]}}],
         "usage":{"prompt_tokens":4,"completion_tokens":6}}
        """;
        var handler = new StubHandler(response);
        var provider = new OpenAiCompatibleChatProviderService(
            new HttpClient(handler), new FakeEnv(new() { ["OPENAI_API_KEY"] = "sk-test" }),
            "gpt", "https://api.openai.com/v1", "OPENAI_API_KEY");

        var reply = await provider.CompleteAsync(new ModelSelection("gpt", "gpt-x", 1024), RequestWithToolTurn());

        // Request: function tools + the assistant tool_calls + a role="tool" result keyed by id.
        Assert.Contains("\"tools\"", handler.CapturedBody);
        Assert.Contains("\"function\"", handler.CapturedBody);
        Assert.Contains("\"tool_calls\"", handler.CapturedBody);
        Assert.Contains("\"tool_call_id\":\"call-1\"", handler.CapturedBody);
        Assert.Contains("\"role\":\"tool\"", handler.CapturedBody);

        // Response: the function call is parsed, arguments string → JsonElement.
        Assert.NotNull(reply.ToolCalls);
        var call = Assert.Single(reply.ToolCalls!);
        Assert.Equal("call_9", call.Id);
        Assert.Equal("search_decisions", call.Name);
        Assert.Equal("danistay", call.Arguments.GetProperty("source").GetString());
    }
}
