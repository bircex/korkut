using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Korkut.Core;

namespace Korkut.Ai;

/// <summary>
/// One embedding adapter for every backend that speaks the OpenAI <c>/embeddings</c> schema —
/// a fully-LOCAL Ollama / HF-TEI endpoint (preferred, so office data never leaves the cluster,
/// see docs/SOURCES.md) or a hosted OpenAI-compatible service. Differs only by base URL, model,
/// and an OPTIONAL API key (local backends need none). The key is read from the environment at
/// call time and sent only in the Authorization header, never stored or logged. The embedding
/// version stamped onto every vector is <c>model:dimension</c>, so a model change re-indexes
/// under a new space (principle 9). HTTP failures surface as <see cref="ModelUnavailableException"/>.
/// </summary>
public sealed class OpenAiCompatibleEmbedder(
    HttpClient http, IEnvironment env, string model, string baseUrl, string? apiKeyEnvVar) : IEmbedder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0)
        {
            return new EmbeddingResult([], model, Version(0), 0, 0);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/embeddings")
        {
            Content = JsonContent.Create(new EmbeddingRequest(model, texts), options: Json),
        };

        // Optional bearer key — only sent when configured (a local backend has none).
        if (apiKeyEnvVar is not null && env.Get(apiKeyEnvVar) is { Length: > 0 } key)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ModelUnavailableException($"Embedding sağlayıcısına ulaşılamadı: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ModelUnavailableException($"Embedding sağlayıcısı hatası ({(int)response.StatusCode}).");
        }

        var body = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(Json, ct);
        if (body?.Data is not { Count: > 0 })
        {
            throw new ModelUnavailableException("Embedding sağlayıcısı boş yanıt döndürdü.");
        }

        // Preserve request order (the API echoes an index per item).
        var vectors = body.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToList();
        var dimensions = vectors[0].Length;
        return new EmbeddingResult(vectors, model, Version(dimensions), dimensions, body.Usage?.PromptTokens ?? 0);
    }

    private string Version(int dimensions) => $"{model}:{dimensions}";

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] List<EmbeddingData>? Data,
        [property: JsonPropertyName("usage")] EmbeddingUsage? Usage);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);

    private sealed record EmbeddingUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens);
}
