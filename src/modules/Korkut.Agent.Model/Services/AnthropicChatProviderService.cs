using Korkut.Contract.Agent.Model;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Core;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// Chat provider for the Anthropic Messages API (provider name "claude"). The API key is
/// read from the environment (<c>ANTHROPIC_API_KEY</c>) at call time and sent only in the
/// request header — never stored, logged, or surfaced. The model id + token cap come from
/// the resolved <see cref="ModelSelection"/>; the caller's <see cref="ChatRequest.MaxTokens"/>
/// can only lower it. Usage (input/output tokens) is returned for cost accounting.
/// </summary>
internal sealed class AnthropicChatProviderService(HttpClient http, IEnvironment env) : IChatProviderService
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    
    private const string AnthropicVersion = "2023-06-01";
    
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => "claude";

    public async Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
    {
        var payload = BuildPayload(selection, request, stream: false);
        using var message = Build(payload, ApiKey());

        var response = await Send(message, HttpCompletionOption.ResponseContentRead, ct);
        var body = await response.Content.ReadFromJsonAsync<AnthropicResponse>(Json, ct);

        var text = string.Concat(body?.Content?.Where(c => c.Type == "text").Select(c => c.Text) ?? []);
        var toolCalls = body?.Content?
            .Where(c => c.Type == "tool_use" && c.Id is not null && c.Name is not null)
            .Select(c => new ToolInvocation(c.Id!, c.Name!, c.Input))
            .ToList();

        if (string.IsNullOrEmpty(text) && (toolCalls is null || toolCalls.Count == 0))
        {
            throw new ModelUnavailableException("Yapay zeka sağlayıcısı boş yanıt döndürdü.");
        }

        var usage = new ChatUsage(Name, selection.ModelId, body!.Usage?.InputTokens ?? 0, body.Usage?.OutputTokens ?? 0);
        return new ChatReply(text, usage, toolCalls is { Count: > 0 } ? toolCalls : null);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ModelSelection selection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = BuildPayload(selection, request, stream: true);
        using var message = Build(payload, ApiKey());

        var response = await Send(message, HttpCompletionOption.ResponseHeadersRead, ct);
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int inputTokens = 0, outputTokens = 0;
        // tool_use blocks stream as content_block_start (id + name) then input_json_delta fragments,
        // keyed by block index; assembled and emitted once so the tool loop can run them.
        var toolAcc = new SortedDictionary<int, ToolAccumulator>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data is "[DONE]")
            {
                break;
            }

            AnthFrame frame;
            try
            {
                frame = ParseFrame(data);
            }
            catch (JsonException)
            {
                continue; // ignore keep-alive / non-JSON frames
            }

            if (frame.InputTokens is { } i)
            {
                inputTokens = i;
            }

            if (frame.OutputTokens is { } o)
            {
                outputTokens = o;
            }

            if (frame.ToolStartIndex is { } ts)
            {
                toolAcc[ts] = new ToolAccumulator { Id = frame.ToolId, Name = frame.ToolName };
            }

            if (frame.ArgsIndex is { } ai && frame.ArgsFragment is { } af
                && toolAcc.TryGetValue(ai, out var acc))
            {
                acc.Args.Append(af);
            }

            if (!string.IsNullOrEmpty(frame.Delta))
            {
                yield return ChatStreamEvent.Text(frame.Delta);
            }
        }

        var calls = toolAcc.Values
            .Where(a => a.Id is not null && a.Name is not null)
            .Select(a => new ToolInvocation(a.Id!, a.Name!, ParseArguments(a.Args.ToString())))
            .ToList();
        if (calls.Count > 0)
        {
            yield return ChatStreamEvent.Tools(calls);
        }

        yield return ChatStreamEvent.Final(new ChatUsage(Name, selection.ModelId, inputTokens, outputTokens));
    }

    private sealed class ToolAccumulator
    {
        public string? Id;
        public string? Name;
        public System.Text.StringBuilder Args { get; } = new();
    }

    private sealed record AnthFrame(
        string? Delta, int? InputTokens, int? OutputTokens,
        int? ToolStartIndex, string? ToolId, string? ToolName, int? ArgsIndex, string? ArgsFragment);

    // message_start → usage.input_tokens; content_block_start(tool_use) → id + name; content_block_delta
    // → delta.text OR delta.input_json_delta.partial_json; message_delta → usage.output_tokens.
    private static AnthFrame ParseFrame(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? delta = null;
        int? toolStartIndex = null, argsIndex = null;
        string? toolId = null, toolName = null, argsFragment = null;

        var index = root.TryGetProperty("index", out var ix) && ix.ValueKind == JsonValueKind.Number ? ix.GetInt32() : 0;

        if (root.TryGetProperty("content_block", out var cb) && cb.ValueKind == JsonValueKind.Object
            && cb.TryGetProperty("type", out var cbt) && cbt.GetString() == "tool_use")
        {
            toolStartIndex = index;
            toolId = cb.TryGetProperty("id", out var idv) ? idv.GetString() : null;
            toolName = cb.TryGetProperty("name", out var nmv) ? nmv.GetString() : null;
        }

        if (root.TryGetProperty("delta", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            if (d.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
            {
                delta = t.GetString();
            }

            if (d.TryGetProperty("partial_json", out var pj) && pj.ValueKind == JsonValueKind.String)
            {
                argsIndex = index;
                argsFragment = pj.GetString();
            }
        }

        int? input = null, output = null;
        if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("usage", out var mu)
            && mu.TryGetProperty("input_tokens", out var it))
        {
            input = it.GetInt32();
        }

        if (root.TryGetProperty("usage", out var u) && u.TryGetProperty("output_tokens", out var ot))
        {
            output = ot.GetInt32();
        }

        return new AnthFrame(delta, input, output, toolStartIndex, toolId, toolName, argsIndex, argsFragment);
    }

    private static JsonElement ParseArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }

        try
        {
            return JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }

    private string ApiKey() => env.Get("ANTHROPIC_API_KEY") is { Length: > 0 } key
        ? key
        : throw new ModelUnavailableException("Anthropic API anahtarı yapılandırılmamış.");

    private static AnthropicRequest BuildPayload(ModelSelection selection, ChatRequest request, bool stream)
    {
        var maxTokens = request.MaxTokens is { } cap ? Math.Min(cap, selection.MaxTokens) : selection.MaxTokens;
        var tools = request.Tools?.Select(t => new AnthropicTool(t.Name, t.Description, t.Schema)).ToList();
        return new AnthropicRequest(
            selection.ModelId,
            maxTokens,
            request.System,
            request.Messages.Select(BuildMessage).ToList(),
            request.Temperature,
            stream ? true : null,
            tools is { Count: > 0 } ? tools : null);
    }

    // Anthropic content is either a plain string or an array of typed blocks. Tool calls (assistant
    // "tool_use") and their results (a user message with "tool_result" blocks) require the array form.
    private static AnthropicMessage BuildMessage(ChatMessage m)
    {
        if (m.Role == "tool" && m.ToolResults is { Count: > 0 } outputs)
        {
            var blocks = outputs.Select(o =>
            {
                var block = new Dictionary<string, object?>
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = o.ToolCallId,
                    ["content"] = o.Content,
                };
                if (o.IsError)
                {
                    block["is_error"] = true;
                }
                return (object)block;
            }).ToList();
            return new AnthropicMessage("user", blocks);
        }

        if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 } calls)
        {
            var blocks = new List<object>();
            if (!string.IsNullOrEmpty(m.Content))
            {
                blocks.Add(new Dictionary<string, object?> { ["type"] = "text", ["text"] = m.Content });
            }
            foreach (var c in calls)
            {
                blocks.Add(new Dictionary<string, object?>
                {
                    ["type"] = "tool_use",
                    ["id"] = c.Id,
                    ["name"] = c.Name,
                    ["input"] = c.Arguments,
                });
            }
            return new AnthropicMessage("assistant", blocks);
        }

        return new AnthropicMessage(m.Role, m.Content);
    }

    private static HttpRequestMessage Build(AnthropicRequest payload, string apiKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(payload, options: Json) };
        message.Headers.Add("x-api-key", apiKey);
        message.Headers.Add("anthropic-version", AnthropicVersion);
        return message;
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage message, HttpCompletionOption option, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, option, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ModelUnavailableException($"Yapay zeka sağlayıcısına ulaşılamadı: {ex.Message}");
        }

        return response.IsSuccessStatusCode
            ? response
            : throw new ModelUnavailableException($"Yapay zeka sağlayıcısı hatası ({(int)response.StatusCode}).");
    }

    private sealed record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("stream")] bool? Stream,
        [property: JsonPropertyName("tools")] IReadOnlyList<AnthropicTool>? Tools);

    // Content is `object`: a plain string for normal turns, or a list of typed blocks for tool_use /
    // tool_result turns. System.Text.Json serializes each shape correctly.
    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] object Content);

    private sealed record AnthropicTool(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("input_schema")] JsonElement InputSchema);

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("content")] List<AnthropicContent>? Content,
        [property: JsonPropertyName("usage")] AnthropicUsage? Usage);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("input")] JsonElement Input);

    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}
