using Korkut.Application.Ai;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Runtime;
using Korkut.Application.Pii;
using Korkut.Infrastructure.Pii;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// The SHARED AI runtime both AI-plane hosts need: the environment adapter + ported RuntimeConfig,
/// the Turkish PII redactor (chat masking in the Agent host + document masking in Knowledge
/// ingestion), the resilient "ai" HttpClient, and the embedder (Knowledge/RAG). The chat side
/// (providers, router, <see cref="IChatModel"/>) lives in the Agent host's own ChatSetup, so this
/// layer carries no chat-model concern. When no embedding endpoint is configured a disabled embedder
/// reports 503 and the host still boots. Called by the Agent and Knowledge hosts.
/// </summary>
public static class AiSetup
{
    public static IServiceCollection AddKorkutAi(this IServiceCollection services)
    {
        services.TryAddSingleton<IEnvironment, SystemEnvironment>();
        services.TryAddSingleton<RuntimeConfig>();

        // Startup-time inspection of the env to decide whether a real embedder is configured.
        var env = new SystemEnvironment();
        var config = new RuntimeConfig(env);

        // Turkish PII redactor — masks cloud-bound chat requests (Agent host) and document text
        // before it is indexed (Knowledge host). Shared, so it is registered here.
        services.TryAddSingleton<IPiiRedactor, PiiRedactor>();

        // Shared HTTP client for AI backends (embedder + the Agent's chat providers) with a
        // retry (backoff+jitter) + circuit-breaker handler. No aggressive attempt timeout — model
        // calls legitimately run for tens of seconds.
        services.AddHttpClient("ai").AddResilienceHandler("korkut-ai", Resilience);

        // Embeddings (Knowledge/RAG): a real OpenAI-compatible adapter when an endpoint is
        // configured (prefer a local Ollama/TEI URL), otherwise disabled (503 on use).
        if (config.HasEmbedding)
        {
            services.AddScoped<IEmbedder>(sp => new OpenAiCompatibleEmbedder(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
                sp.GetRequiredService<IEnvironment>(),
                config.EmbeddingModel, config.EmbeddingBaseUrl!, "KORKUT_EMBEDDING_API_KEY"));
        }
        else
        {
            services.AddScoped<IEmbedder, DisabledEmbedder>();
        }

        return services;
    }

    // Retry (exponential backoff + jitter) + circuit-breaker around backend calls.
    internal static void Resilience(ResiliencePipelineBuilder<HttpResponseMessage> builder) => builder
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions());
}
