using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Application.Ai;
using Korkut.Agent.Application.Ai;
using Korkut.Agent.Application.Abstractions.Ai;
using Korkut.Agent.Application.Abstractions.Persistence;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Application.Runtime;

namespace Korkut.Agent.Infrastructure.Ai.Providers;

/// <summary>
/// Chat provider for the Anthropic Messages API (provider name "claude"). The API key is
/// read from the environment (<c>ANTHROPIC_API_KEY</c>) at call time and sent only in the
/// request header — never stored, logged, or surfaced. The model id + token cap come from
/// the resolved <see cref="ModelSelection"/>; the caller's <see cref="ChatRequest.MaxTokens"/>
/// can only lower it. Usage (input/output tokens) is returned for cost accounting.
/// </summary>
public sealed class AnthropicChatProvider(HttpClient http, IEnvironment env) : IChatProvider
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
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data is "[DONE]")
            {
                break;
            }

            (string? delta, int? inTok, int? outTok) frame;
            try
            {
                frame = ParseFrame(data);
            }
            catch (JsonException)
            {
                continue; // ignore keep-alive / non-JSON frames
            }

            if (frame.inTok is { } i)
            {
                inputTokens = i;
            }

            if (frame.outTok is { } o)
            {
                outputTokens = o;
            }

            if (!string.IsNullOrEmpty(frame.delta))
            {
                yield return ChatStreamEvent.Text(frame.delta);
            }
        }

        yield return ChatStreamEvent.Final(new ChatUsage(Name, selection.ModelId, inputTokens, outputTokens));
    }

    // message_start carries usage.input_tokens; content_block_delta carries delta.text;
    // message_delta carries usage.output_tokens.
    private static (string? Delta, int? InputTokens, int? OutputTokens) ParseFrame(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? delta = null;
        if (root.TryGetProperty("delta", out var d) && d.TryGetProperty("text", out var t))
        {
            delta = t.GetString();
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

        return (delta, input, output);
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
