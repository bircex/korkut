using Korkut.Contract.Agent.Model;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Core;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// One adapter for every provider that speaks the OpenAI chat-completions schema —
/// OpenAI ("gpt"), Groq ("groq"), and local Ollama/vLLM ("ollama") — differing only by
/// base URL, API-key env var, and model. Instantiated once per provider in DI. Keys are
/// read from the environment at call time and sent only in the Authorization header; a
/// local backend needs no key.
/// </summary>
public sealed class OpenAiCompatibleChatProviderService(
    HttpClient http, IEnvironment env, string name, string baseUrl, string? apiKeyEnvVar) : IChatProviderService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => name;

    public async Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
    {
        using var message = Build(BuildPayload(selection, request, stream: false));
        var response = await Send(message, HttpCompletionOption.ResponseContentRead, ct);
        var body = await response.Content.ReadFromJsonAsync<CompletionResponse>(Json, ct);
        var replyMessage = body?.Choices?.FirstOrDefault()?.Message;
        var text = replyMessage?.Content;
        var toolCalls = replyMessage?.ToolCalls?
            .Where(t => t.Id is not null && t.Function is not null)
            .Select(t => new ToolInvocation(t.Id!, t.Function!.Name, ParseArguments(t.Function.Arguments)))
            .ToList();

        if (string.IsNullOrEmpty(text) && (toolCalls is null || toolCalls.Count == 0))
        {
            throw new ModelUnavailableException("Yapay zeka sağlayıcısı boş yanıt döndürdü.");
        }

        var usage = new ChatUsage(name, selection.ModelId, body!.Usage?.PromptTokens ?? 0, body.Usage?.CompletionTokens ?? 0);
        return new ChatReply(text ?? string.Empty, usage, toolCalls is { Count: > 0 } ? toolCalls : null);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ModelSelection selection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var message = Build(BuildPayload(selection, request, stream: true));
        var response = await Send(message, HttpCompletionOption.ResponseHeadersRead, ct);
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int prompt = 0, completion = 0;
        // Tool calls stream incrementally (id + name in the first delta, arguments across many),
        // keyed by index; assembled and emitted once at the end so the loop can run them.
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

            StreamFrame frame;
            try
            {
                frame = ParseFrame(data);
            }
            catch (JsonException)
            {
                continue;
            }

            if (frame.Prompt is { } pv)
            {
                prompt = pv;
            }

            if (frame.Completion is { } cv)
            {
                completion = cv;
            }

            foreach (var f in frame.ToolFragments)
            {
                if (!toolAcc.TryGetValue(f.Index, out var acc))
                {
                    toolAcc[f.Index] = acc = new ToolAccumulator();
                }

                if (f.Id is not null)
                {
                    acc.Id = f.Id;
                }

                if (f.Name is not null)
                {
                    acc.Name = f.Name;
                }

                if (f.ArgsFragment is not null)
                {
                    acc.Args.Append(f.ArgsFragment);
                }
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

        yield return ChatStreamEvent.Final(new ChatUsage(name, selection.ModelId, prompt, completion));
    }

    private sealed class ToolAccumulator
    {
        public string? Id;
        public string? Name;
        public System.Text.StringBuilder Args { get; } = new();
    }

    private sealed record ToolFragment(int Index, string? Id, string? Name, string? ArgsFragment);

    private sealed record StreamFrame(
        string? Delta, int? Prompt, int? Completion, IReadOnlyList<ToolFragment> ToolFragments);

    // choices[0].delta carries content (text) and/or tool_calls fragments; a terminal frame may
    // carry usage.{prompt,completion}_tokens.
    private static StreamFrame ParseFrame(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? delta = null;
        var fragments = new List<ToolFragment>();
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("delta", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            if (d.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                delta = content.GetString();
            }

            if (d.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in tcs.EnumerateArray())
                {
                    var index = tc.TryGetProperty("index", out var ix) && ix.ValueKind == JsonValueKind.Number ? ix.GetInt32() : 0;
                    var id = tc.TryGetProperty("id", out var idv) && idv.ValueKind == JsonValueKind.String ? idv.GetString() : null;
                    string? fname = null, fargs = null;
                    if (tc.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                    {
                        if (fn.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                        {
                            fname = nm.GetString();
                        }

                        if (fn.TryGetProperty("arguments", out var ar) && ar.ValueKind == JsonValueKind.String)
                        {
                            fargs = ar.GetString();
                        }
                    }

                    fragments.Add(new ToolFragment(index, id, fname, fargs));
                }
            }
        }

        int? prompt = null, completion = null;
        if (root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            if (u.TryGetProperty("prompt_tokens", out var pt))
            {
                prompt = pt.GetInt32();
            }

            if (u.TryGetProperty("completion_tokens", out var ctk))
            {
                completion = ctk.GetInt32();
            }
        }

        return new StreamFrame(delta, prompt, completion, fragments);
    }

    private static CompletionRequest BuildPayload(ModelSelection selection, ChatRequest request, bool stream)
    {
        var maxTokens = request.MaxTokens is { } cap ? Math.Min(cap, selection.MaxTokens) : selection.MaxTokens;
        var messages = new List<CompletionMessage>(request.Messages.Count + 1)
        {
            new("system", request.System),
        };
        foreach (var m in request.Messages)
        {
            AppendMessage(messages, m);
        }

        var tools = request.Tools?
            .Select(t => new CompletionTool("function", new CompletionFunction(t.Name, t.Description, t.Schema)))
            .ToList();

        return new CompletionRequest(
            selection.ModelId, maxTokens, messages, request.Temperature, stream ? true : null,
            stream ? new StreamOptions(true) : null,
            tools is { Count: > 0 } ? tools : null);
    }

    // OpenAI carries tool calls on the assistant message (tool_calls, arguments as a JSON string) and
    // returns each tool result as its own role="tool" message keyed by tool_call_id.
    private static void AppendMessage(List<CompletionMessage> messages, ChatMessage m)
    {
        if (m.Role == "tool" && m.ToolResults is { Count: > 0 } outputs)
        {
            foreach (var o in outputs)
            {
                messages.Add(new CompletionMessage("tool", o.Content, ToolCallId: o.ToolCallId));
            }
            return;
        }

        if (m.Role == "assistant" && m.ToolCalls is { Count: > 0 } calls)
        {
            var toolCalls = calls
                .Select(c => new ToolCallDto(c.Id, "function", new FunctionCallDto(c.Name, c.Arguments.GetRawText())))
                .ToList();
            messages.Add(new CompletionMessage("assistant",
                string.IsNullOrEmpty(m.Content) ? null : m.Content, ToolCalls: toolCalls));
            return;
        }

        messages.Add(new CompletionMessage(m.Role, m.Content));
    }

    private static JsonElement ParseArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return EmptyObject;
        }
        try
        {
            return JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            return EmptyObject;
        }
    }

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    private HttpRequestMessage Build(CompletionRequest payload)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(payload, options: Json),
        };
        if (apiKeyEnvVar is not null)
        {
            var key = env.Get(apiKeyEnvVar);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ModelUnavailableException($"{name} API anahtarı yapılandırılmamış.");
            }

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

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

    private sealed record CompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] IReadOnlyList<CompletionMessage> Messages,
        [property: JsonPropertyName("temperature")] double? Temperature,
        [property: JsonPropertyName("stream")] bool? Stream,
        [property: JsonPropertyName("stream_options")] StreamOptions? StreamOptions,
        [property: JsonPropertyName("tools"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CompletionTool>? Tools);

    private sealed record StreamOptions([property: JsonPropertyName("include_usage")] bool IncludeUsage);

    private sealed record CompletionTool(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] CompletionFunction Function);

    private sealed record CompletionFunction(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] JsonElement Parameters);

    private sealed record CompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ToolCallDto>? ToolCalls = null,
        [property: JsonPropertyName("tool_call_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ToolCallId = null);

    private sealed record ToolCallDto(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] FunctionCallDto? Function);

    private sealed record FunctionCallDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] string? Arguments);

    private sealed record CompletionResponse(
        [property: JsonPropertyName("choices")] List<CompletionChoice>? Choices,
        [property: JsonPropertyName("usage")] CompletionUsage? Usage);

    private sealed record CompletionChoice(
        [property: JsonPropertyName("message")] CompletionMessage? Message);

    private sealed record CompletionUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
