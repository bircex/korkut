using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Application.Ai;
using Korkut.Application.Runtime;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// One adapter for every provider that speaks the OpenAI chat-completions schema —
/// OpenAI ("gpt"), Groq ("groq"), and local Ollama/vLLM ("ollama") — differing only by
/// base URL, API-key env var, and model. Instantiated once per provider in DI. Keys are
/// read from the environment at call time and sent only in the Authorization header; a
/// local backend needs no key.
/// </summary>
public sealed class OpenAiCompatibleChatProvider(
    HttpClient http, IEnvironment env, string name, string baseUrl, string? apiKeyEnvVar) : IChatProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => name;

    public async Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
    {
        using var message = Build(BuildPayload(selection, request, stream: false));
        var response = await Send(message, HttpCompletionOption.ResponseContentRead, ct);
        var body = await response.Content.ReadFromJsonAsync<CompletionResponse>(Json, ct);
        var text = body?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrEmpty(text))
        {
            throw new ModelUnavailableException("Yapay zeka sağlayıcısı boş yanıt döndürdü.");
        }

        var usage = new ChatUsage(name, selection.ModelId, body!.Usage?.PromptTokens ?? 0, body.Usage?.CompletionTokens ?? 0);
        return new ChatReply(text, usage);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ModelSelection selection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var message = Build(BuildPayload(selection, request, stream: true));
        var response = await Send(message, HttpCompletionOption.ResponseHeadersRead, ct);
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int prompt = 0, completion = 0;
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

            (string? delta, int? p, int? c) frame;
            try
            {
                frame = ParseFrame(data);
            }
            catch (JsonException)
            {
                continue;
            }

            if (frame.p is { } pv)
            {
                prompt = pv;
            }

            if (frame.c is { } cv)
            {
                completion = cv;
            }

            if (!string.IsNullOrEmpty(frame.delta))
            {
                yield return ChatStreamEvent.Text(frame.delta);
            }
        }

        yield return ChatStreamEvent.Final(new ChatUsage(name, selection.ModelId, prompt, completion));
    }

    // choices[0].delta.content = text; a terminal frame may carry usage.{prompt,completion}_tokens.
    private static (string? Delta, int? Prompt, int? Completion) ParseFrame(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        string? delta = null;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
            && choices[0].TryGetProperty("delta", out var d) && d.TryGetProperty("content", out var content))
        {
            delta = content.GetString();
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

        return (delta, prompt, completion);
    }

    private static CompletionRequest BuildPayload(ModelSelection selection, ChatRequest request, bool stream)
    {
        var maxTokens = request.MaxTokens is { } cap ? Math.Min(cap, selection.MaxTokens) : selection.MaxTokens;
        var messages = new List<CompletionMessage>(request.Messages.Count + 1)
        {
            new("system", request.System),
        };
        messages.AddRange(request.Messages.Select(m => new CompletionMessage(m.Role, m.Content)));
        return new CompletionRequest(
            selection.ModelId, maxTokens, messages, request.Temperature, stream ? true : null,
            stream ? new StreamOptions(true) : null);
    }

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
        [property: JsonPropertyName("stream_options")] StreamOptions? StreamOptions);

    private sealed record StreamOptions([property: JsonPropertyName("include_usage")] bool IncludeUsage);

    private sealed record CompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record CompletionResponse(
        [property: JsonPropertyName("choices")] List<CompletionChoice>? Choices,
        [property: JsonPropertyName("usage")] CompletionUsage? Usage);

    private sealed record CompletionChoice(
        [property: JsonPropertyName("message")] CompletionMessage? Message);

    private sealed record CompletionUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}
