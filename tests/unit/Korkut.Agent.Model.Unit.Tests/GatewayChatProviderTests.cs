using System.Net;
using System.Text;
using Korkut.Agent.Model.Services;
using Korkut.Contract.Agent.Model;
using Korkut.Core;

namespace Korkut.Agent.Model.Tests;

/// <summary>
/// The chat adapter against what the model gateway (llmr) actually sends: its cost, its error
/// envelope, and its stream contract (a complete stream ends with [DONE]; a failure after the first
/// byte arrives as an error frame). A stand-in gateway replays canned responses.
/// </summary>
public class GatewayChatProviderTests
{
    private sealed class Env(Dictionary<string, string> values) : IEnvironment
    {
        public string? Get(string name) => values.GetValueOrDefault(name);
    }

    private sealed class Gateway(HttpStatusCode status, string body, string contentType = "application/json") : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) });
        }
    }

    private static readonly ModelSelection Selection = new("llmr", "korkut-chat", 1024);

    private static ChatRequest Ask() => new("sistem", [new ChatMessage("user", "merhaba")], ModelSpecialty.Dispatcher);

    private static OpenAiCompatibleChatProviderService Provider(Gateway gateway, string baseUrl = "http://llmr:8080/v1")
        => new(new HttpClient(gateway), new Env(new() { ["KORKUT_LLMR_TOKEN"] = "gateway-token" }), "llmr", baseUrl, "KORKUT_LLMR_TOKEN");

    private static string Sse(params string[] frames) => string.Concat(frames.Select(f => $"data: {f}\n\n"));

    private static async Task<List<ChatStreamEvent>> Drain(IAsyncEnumerable<ChatStreamEvent> stream)
    {
        var events = new List<ChatStreamEvent>();
        await foreach (var e in stream)
        {
            events.Add(e);
        }

        return events;
    }

    private const string Hello = """{"choices":[{"index":0,"delta":{"content":"Merhaba"}}]}""";

    [Fact]
    public async Task A_complete_stream_yields_text_usage_and_the_gateways_cost()
    {
        var gateway = new Gateway(HttpStatusCode.OK, Sse(
            Hello,
            """{"choices":[],"usage":{"prompt_tokens":12,"completion_tokens":3},"llmr_cost":{"status":"priced","amount":"0.002264","currency":"USD"}}""",
            "[DONE]"), "text/event-stream");

        var events = await Drain(Provider(gateway).StreamAsync(Selection, Ask()));

        Assert.Equal("Merhaba", string.Concat(events.Select(e => e.Delta)));
        var usage = events.Last().Usage!;
        Assert.Equal((12, 3, 0.002264m), (usage.PromptTokens, usage.CompletionTokens, usage.CostUsd));
        Assert.Equal("Bearer gateway-token", gateway.Last!.Headers.Authorization!.ToString());
        Assert.Equal("http://llmr:8080/v1/chat/completions", gateway.Last.RequestUri!.ToString());
    }

    [Fact]
    public async Task A_stream_that_breaks_after_it_started_is_an_error_not_an_answer()
    {
        var gateway = new Gateway(HttpStatusCode.OK, Sse(Hello, """{"error":{"message":"upstream closed","type":"api_error"}}"""), "text/event-stream");

        var ex = await Assert.ThrowsAsync<ModelUnavailableException>(() => Drain(Provider(gateway).StreamAsync(Selection, Ask())));

        Assert.Contains("upstream closed", ex.Message);
    }

    [Fact]
    public async Task A_stream_that_just_stops_without_done_is_cut_short_not_finished()
    {
        var gateway = new Gateway(HttpStatusCode.OK, Sse(Hello), "text/event-stream");

        var ex = await Assert.ThrowsAsync<ModelUnavailableException>(() => Drain(Provider(gateway).StreamAsync(Selection, Ask())));

        Assert.Contains("yarıda kesildi", ex.Message);
    }

    [Theory]
    [InlineData("""{"status":"partial","amount":"0.001","currency":"USD"}""")]
    [InlineData("""{"status":"unpriced"}""")]
    [InlineData("""{"status":"priced","amount":"0.5","currency":"EUR"}""")]
    public async Task A_cost_the_gateway_could_not_fully_price_in_dollars_stays_unknown(string cost)
    {
        var gateway = new Gateway(HttpStatusCode.OK,
            $$$"""{"choices":[{"message":{"role":"assistant","content":"Tamam"}}],"usage":{"prompt_tokens":1,"completion_tokens":1},"llmr_cost":{{{cost}}}}""");

        var reply = await Provider(gateway).CompleteAsync(Selection, Ask());

        Assert.Null(reply.Usage.CostUsd);
    }

    [Fact]
    public async Task A_self_hosted_model_is_free()
    {
        var gateway = new Gateway(HttpStatusCode.OK,
            """{"choices":[{"message":{"role":"assistant","content":"Tamam"}}],"usage":{"prompt_tokens":1,"completion_tokens":1},"llmr_cost":{"status":"free"}}""");

        Assert.Equal(0m, (await Provider(gateway).CompleteAsync(Selection, Ask())).Usage.CostUsd);
    }

    [Theory]
    [InlineData("model_not_found")]
    [InlineData("model_not_enabled")]
    [InlineData("no_route")]
    public async Task No_model_chosen_yet_points_at_the_ai_settings(string code)
    {
        var gateway = new Gateway(HttpStatusCode.NotFound,
            $$$"""{"error":{"message":"no model named korkut-chat","type":"invalid_request_error","code":"{{{code}}}"}}""");

        var ex = await Assert.ThrowsAsync<ModelUnavailableException>(() => Provider(gateway).CompleteAsync(Selection, Ask()));

        Assert.Contains("Ayarlar → Yapay zeka", ex.Message);
    }

    [Fact]
    public async Task Any_other_refusal_carries_the_gateways_message()
    {
        var gateway = new Gateway(HttpStatusCode.BadRequest,
            """{"error":{"message":"stop is not supported","type":"invalid_request_error","param":"stop","code":"unsupported"}}""");

        var ex = await Assert.ThrowsAsync<ModelUnavailableException>(() => Provider(gateway).CompleteAsync(Selection, Ask()));

        Assert.Contains("400", ex.Message);
        Assert.Contains("stop is not supported", ex.Message);
    }

    [Fact]
    public async Task Without_a_gateway_address_the_call_says_so_and_sends_nothing()
    {
        var gateway = new Gateway(HttpStatusCode.OK, "{}");

        var ex = await Assert.ThrowsAsync<ModelUnavailableException>(() => Provider(gateway, baseUrl: "").CompleteAsync(Selection, Ask()));

        Assert.Contains("llmr", ex.Message);
        Assert.Null(gateway.Last);
    }
}
