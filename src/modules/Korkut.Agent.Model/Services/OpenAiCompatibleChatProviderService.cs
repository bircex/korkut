using Korkut.Contract.Agent.Model;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Core;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// The chat adapter for the OpenAI chat-completions schema, which Korkut speaks to the model
/// gateway (llmr): every model call goes through it, whichever provider llmr routes to. The token
/// is read from the environment at call time and sent only in the Authorization header. Beyond
/// the plain schema it reads what llmr adds: the call's cost (<c>llmr_cost</c>), its error
/// envelope, and the rule that a stream which ends without <c>[DONE]</c> was cut short.
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

        // Some models (deepseek-chat) emit their tool call as TEXT instead of structured tool_calls.
        // Salvage it so the tool actually runs and the raw markup never reaches the user.
        if ((toolCalls is null || toolCalls.Count == 0) && LeakedToolCallParser.LooksLikeToolCall(text))
        {
            var salvaged = LeakedToolCallParser.Parse(text!).ToList();
            text = LeakedToolCallParser.Strip(text!);
            if (salvaged.Count > 0)
            {
                toolCalls = salvaged;
            }
        }

        if (string.IsNullOrEmpty(text) && (toolCalls is null || toolCalls.Count == 0))
        {
            throw new ModelUnavailableException("Yapay zeka sağlayıcısı boş yanıt döndürdü.");
        }

        var usage = new ChatUsage(
            name, selection.ModelId, body!.Usage?.PromptTokens ?? 0, body.Usage?.CompletionTokens ?? 0, body.Cost?.Usd);
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
        decimal? cost = null;
        // llmr ends every complete stream with [DONE]; one that just stops was cut off mid-answer.
        var finished = false;
        // Tool calls stream incrementally (id + name in the first delta, arguments across many),
        // keyed by index; assembled and emitted once at the end so the loop can run them.
        var toolAcc = new SortedDictionary<int, ToolAccumulator>();
        // Guards the content channel: holds text back just long enough to tell a normal answer from a
        // leaked text tool-call block (deepseek-chat), so the raw markup is never streamed to the user.
        var gate = new ContentGate();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data is "[DONE]")
            {
                finished = true;
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

            if (frame.Error is { } error)
            {
                // A failure after the first byte: llmr cannot fall over to another route by then.
                throw new ModelUnavailableException($"Yapay zeka yanıtı yarıda kesildi: {error}");
            }

            if (frame.Cost is { } priced)
            {
                cost = priced.Usd;
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
                foreach (var chunk in gate.Push(frame.Delta))
                {
                    yield return ChatStreamEvent.Text(chunk);
                }
            }
        }

        if (!finished)
        {
            throw new ModelUnavailableException("Yapay zeka yanıtı yarıda kesildi: bağlantı tamamlanmadan kapandı.");
        }

        var calls = toolAcc.Values
            .Where(a => a.Id is not null && a.Name is not null)
            .Select(a => new ToolInvocation(a.Id!, a.Name!, ParseArguments(a.Args.ToString())))
            .ToList();

        // Flush the gate: a held leaked block becomes real tool calls (never shown); otherwise any
        // remaining buffered prose is emitted as the tail of the answer.
        var (tail, leaked) = gate.Finish();
        if (calls.Count == 0 && leaked is not null && LeakedToolCallParser.LooksLikeToolCall(leaked))
        {
            calls = LeakedToolCallParser.Parse(leaked).ToList();
        }
        else if (!string.IsNullOrEmpty(tail))
        {
            yield return ChatStreamEvent.Text(tail);
        }

        if (calls.Count > 0)
        {
            yield return ChatStreamEvent.Tools(calls);
        }

        yield return ChatStreamEvent.Final(new ChatUsage(name, selection.ModelId, prompt, completion, cost));
    }

    private sealed class ToolAccumulator
    {
        public string? Id;
        public string? Name;
        public System.Text.StringBuilder Args { get; } = new();
    }

    /// <summary>
    /// Streams normal answer text live but withholds a leaked text tool-call block — even when it
    /// appears AFTER real prose (deepseek-chat often writes a sentence, then the block). It keeps the
    /// full content, streams everything up to the first tool-call marker (retaining a small guard tail
    /// so a marker split across deltas is still caught), and captures from the marker on. At the end a
    /// captured block that really parses is handed to the tool parser; a false positive is emitted as
    /// text after all, so genuine content is never dropped.
    /// </summary>
    private sealed class ContentGate
    {
        // Retain this many trailing chars unemitted so a marker straddling two deltas is still found
        // (the markers we key off — "tool_calls" / "invoke name=" — are shorter than this).
        private const int Guard = 16;

        private readonly System.Text.StringBuilder _content = new();
        private int _emitted;
        private int _blockStart = -1;

        public IEnumerable<string> Push(string delta)
        {
            _content.Append(delta);
            if (_blockStart >= 0)
            {
                yield break; // already capturing a leaked block
            }

            var s = _content.ToString();
            var mark = EarliestMarker(s);
            if (mark >= 0)
            {
                var open = s.LastIndexOf('<', Math.Min(mark, s.Length - 1));
                _blockStart = open >= 0 ? open : mark;
                if (_blockStart > _emitted)
                {
                    var prose = s[_emitted.._blockStart];
                    _emitted = _blockStart;
                    if (prose.Length > 0)
                    {
                        yield return prose;
                    }
                }

                yield break;
            }

            var safeEnd = s.Length - Guard;
            if (safeEnd > _emitted)
            {
                var chunk = s[_emitted..safeEnd];
                _emitted = safeEnd;
                yield return chunk;
            }
        }

        public (string Tail, string? Leaked) Finish()
        {
            var s = _content.ToString();
            if (_blockStart >= 0)
            {
                var block = s[_blockStart..];
                // Genuine leak → parse it; false positive (marker word in prose) → show the held text.
                return LeakedToolCallParser.LooksLikeToolCall(block) ? (string.Empty, block) : (s[_emitted..], null);
            }

            return (_emitted < s.Length ? s[_emitted..] : string.Empty, null);
        }

        private static int EarliestMarker(string s)
        {
            var a = s.IndexOf("invoke name=", StringComparison.Ordinal);
            var b = s.IndexOf("tool_calls", StringComparison.Ordinal);
            if (a < 0)
            {
                return b;
            }

            return b < 0 ? a : Math.Min(a, b);
        }
    }

    private sealed record ToolFragment(int Index, string? Id, string? Name, string? ArgsFragment);

    private sealed record StreamFrame(
        string? Delta, int? Prompt, int? Completion, IReadOnlyList<ToolFragment> ToolFragments,
        string? Error = null, GatewayCost? Cost = null);

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

        // llmr's last frame before [DONE] carries the cost; a frame that is only an error means the
        // stream broke after it started.
        var cost = root.TryGetProperty("llmr_cost", out var c) && c.ValueKind == JsonValueKind.Object
            ? c.Deserialize<GatewayCost>(Json)
            : null;
        string? error = null;
        if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object)
        {
            error = e.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : "bilinmeyen hata";
        }

        return new StreamFrame(delta, prompt, completion, fragments, error, cost);
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
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ModelUnavailableException("Yapay zeka model geçidi (llmr) yapılandırılmamış.");
        }

        var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(payload, options: Json),
        };
        if (apiKeyEnvVar is not null && env.Get(apiKeyEnvVar) is { Length: > 0 } key)
        {
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

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        throw new ModelUnavailableException(await DescribeFailureAsync(response, ct));
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

    /// <summary>
    /// What to tell the user when the gateway refuses. A model that is not chosen yet (no route set,
    /// nothing enabled, or every route unusable) is a setup step, not a fault, so it points at the
    /// settings screen; anything else carries llmr's own message.
    /// </summary>
    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            GatewayError? error = null;
            try
            {
                error = (await response.Content.ReadFromJsonAsync<GatewayErrorEnvelope>(Json, ct))?.Error;
            }
            catch (JsonException)
            {
            }

            return error?.Code switch
            {
                "model_not_found" or "model_not_enabled" or "no_route" =>
                    "Yapay zeka modeli seçilmemiş ya da kullanılamıyor. Ofis sahibi Ayarlar → Yapay zeka bölümünden bir sohbet modeli seçmeli.",
                _ when error?.Message is { Length: > 0 } message => $"Yapay zeka sağlayıcısı hatası ({(int)response.StatusCode}): {message}",
                _ => $"Yapay zeka sağlayıcısı hatası ({(int)response.StatusCode}).",
            };
        }
    }

    private sealed record GatewayErrorEnvelope([property: JsonPropertyName("error")] GatewayError? Error);

    private sealed record GatewayError(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("code")] string? Code);

    /// <summary>
    /// llmr's price for one call. Only a fully priced amount in US dollars becomes a cost: a
    /// self-hosted model is genuinely free, and a partial or unpriced figure stays unknown rather
    /// than being recorded as a bill that is too low.
    /// </summary>
    private sealed record GatewayCost(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("amount")] string? Amount,
        [property: JsonPropertyName("currency")] string? Currency)
    {
        public decimal? Usd => Status switch
        {
            "free" => 0m,
            "priced" when Currency == "USD" && decimal.TryParse(
                Amount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var usd) => usd,
            _ => null,
        };
    }

    private sealed record CompletionResponse(
        [property: JsonPropertyName("choices")] List<CompletionChoice>? Choices,
        [property: JsonPropertyName("usage")] CompletionUsage? Usage,
        [property: JsonPropertyName("llmr_cost")] GatewayCost? Cost = null);

    private sealed record CompletionChoice(
        [property: JsonPropertyName("message")] CompletionMessage? Message);

    private sealed record CompletionUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
