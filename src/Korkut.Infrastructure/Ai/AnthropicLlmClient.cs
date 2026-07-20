using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Application.Ai;
using Korkut.Application.Runtime;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// Chat completions via the Anthropic Messages API. The API key is read from the
/// environment (<c>ANTHROPIC_API_KEY</c>) at call time and sent only in the request
/// header — never stored, logged, or surfaced. Model + token cap come from
/// <see cref="RuntimeConfig"/> (the ported ENV contract).
/// </summary>
public sealed class AnthropicLlmClient(HttpClient http, RuntimeConfig config, IEnvironment env) : ILlmClient
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<LlmReply> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var apiKey = env.Get("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new LlmUnavailableException("Anthropic API anahtarı yapılandırılmamış.");
        }

        var payload = new AnthropicRequest(
            config.AnthropicModel,
            Math.Min(request.MaxTokens, config.AnthropicMaxTokens),
            request.System,
            request.Messages.Select(m => new AnthropicMessage(m.Role, m.Content)).ToList());

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload, options: Json),
        };
        message.Headers.Add("x-api-key", apiKey);
        message.Headers.Add("anthropic-version", AnthropicVersion);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmUnavailableException($"Yapay zeka sağlayıcısına ulaşılamadı: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new LlmUnavailableException($"Yapay zeka sağlayıcısı hatası ({(int)response.StatusCode}).");
        }

        var body = await response.Content.ReadFromJsonAsync<AnthropicResponse>(Json, ct);
        var text = body?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
        return string.IsNullOrEmpty(text)
            ? throw new LlmUnavailableException("Yapay zeka sağlayıcısı boş yanıt döndürdü.")
            : new LlmReply(text);
    }

    private sealed record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record AnthropicResponse(
        [property: JsonPropertyName("content")] List<AnthropicContent>? Content);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}

/// <summary>Fallback client used when no provider key is configured — every call reports unavailable.</summary>
public sealed class DisabledLlmClient : ILlmClient
{
    public Task<LlmReply> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
        throw new LlmUnavailableException("Yapay zeka sağlayıcısı yapılandırılmamış.");
}
