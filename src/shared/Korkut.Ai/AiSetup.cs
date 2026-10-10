using Korkut.Contract.Pii;
using Korkut.Core;
using Korkut.Pii;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Ai;

/// <summary>
/// The shared AI runtime: the environment adapter and <see cref="RuntimeConfig"/>, the Turkish PII
/// redactor (chat masking and document masking before indexing), the "ai" HttpClient for the model
/// gateway, and the embedder for document search. The chat side (the provider and the
/// <see cref="Korkut.Contract.Agent.Model.IChatModelService"/> facade) is registered by the Agent
/// model module. Without a gateway the embedder is disabled (503 on use) and the host still boots.
/// </summary>
public static class AiSetup
{
    /// <summary>The route set name document search embeds with.</summary>
    public const string EmbedRoute = "korkut-embed";

    public static IServiceCollection AddKorkutAi(this IServiceCollection services)
    {
        services.TryAddSingleton<IEnvironment, SystemEnvironment>();
        services.TryAddSingleton<RuntimeConfig>();

        // Turkish PII redactor: masks model-bound chat requests and document text before it is
        // indexed. Shared, so it is registered here.
        services.TryAddSingleton<IPiiRedactorService, PiiRedactorService>();

        // The client for the model gateway (llmr), used by the chat provider and the embedder. No
        // retry or circuit breaker here: llmr retries, falls back across routes and rests failing
        // ones itself, and a second layer would multiply every retry. The timeout leaves room for a
        // long draft.
        services.AddHttpClient("ai", client => client.Timeout = TimeSpan.FromMinutes(5));

        // Embeddings for document search go to llmr's korkut-embed route set, chosen in the AI
        // settings. Without a gateway, embeddings are disabled (503 on use).
        services.AddScoped<IEmbedder>(sp => sp.GetRequiredService<RuntimeConfig>().LlmrUrl is { } url
            ? new OpenAiCompatibleEmbedder(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
                sp.GetRequiredService<IEnvironment>(),
                EmbedRoute, $"{url}/v1", "KORKUT_LLMR_TOKEN")
            : new DisabledEmbedder());

        return services;
    }
}
