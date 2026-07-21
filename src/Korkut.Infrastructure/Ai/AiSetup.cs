using Korkut.Application.Ai;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Runtime;
using Korkut.Pii;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// Wires the swappable model runtime: the environment adapter + ported RuntimeConfig, the
/// model-routing rules, the per-provider chat adapters (registered only when configured),
/// and the routing facade consumers depend on (<see cref="IChatModel"/>). If no provider is
/// configured, a disabled model reports "unavailable" (503) so the host still boots. Adding
/// a provider is one registration here + config — no consumer changes. Tests swap in a fake
/// <see cref="IChatProvider"/> / <see cref="IChatModel"/>.
/// </summary>
public static class AiSetup
{
    public static IServiceCollection AddKorkutAi(this IServiceCollection services)
    {
        services.TryAddSingleton<IEnvironment, SystemEnvironment>();
        services.TryAddSingleton<RuntimeConfig>();

        // Startup-time inspection of the env to decide which providers to register (keys are
        // set at boot). The DI IEnvironment/RuntimeConfig above are what runs at request time.
        var env = new SystemEnvironment();
        var config = new RuntimeConfig(env);

        services.AddSingleton(ModelRoutingLoader.LoadOrDefault(env));
        services.AddSingleton<ILocalModelProbe, LocalModelUnavailable>();
        services.AddSingleton<IModelRouter, ModelRouter>();

        // Turkish PII redactor — used to mask cloud-bound chat requests (below) and, in the
        // Knowledge host, to mask document text before it is indexed (legal-product requirement).
        services.TryAddSingleton<IPiiRedactor, PiiRedactor>();

        // --- Chat providers, keyed by their model-routing.yaml token, registered only when
        //     configured so the router never resolves an unwired provider. Cloud providers are
        //     wrapped so raw PII is masked before it leaves the office; local providers are not. ---
        services.AddHttpClient("ai").AddResilienceHandler("korkut-ai", Resilience);

        if (config.HasAnthropicKey)
        {
            services.AddHttpClient<AnthropicChatProvider>().AddResilienceHandler("korkut-anthropic", Resilience);
            services.AddTransient<IChatProvider>(sp => Cloud(sp, sp.GetRequiredService<AnthropicChatProvider>()));
        }

        if (config.HasOpenAiKey)
        {
            services.AddTransient<IChatProvider>(sp => Cloud(sp, Compat(sp, "gpt", "https://api.openai.com/v1", "OPENAI_API_KEY")));
        }

        if (config.HasGroqKey)
        {
            services.AddTransient<IChatProvider>(sp => Cloud(sp, Compat(sp, "groq", "https://api.groq.com/openai/v1", "GROQ_API_KEY")));
        }

        // Local (Ollama/vLLM) only when the operator opts in with a base URL. NOT wrapped: an
        // on-prem endpoint keeps the data in-office, so redaction would only cost fidelity.
        if (env.Get("KORKUT_OLLAMA_URL") is { Length: > 0 } ollamaUrl)
        {
            services.AddTransient<IChatProvider>(sp => Compat(sp, "ollama", ollamaUrl, apiKeyEnvVar: null));
        }

        // The facade: route when at least one provider is wired, otherwise report unavailable.
        services.AddScoped<IChatModel>(sp =>
        {
            var providers = sp.GetServices<IChatProvider>().ToList();
            return providers.Count == 0
                ? new DisabledChatModel()
                : new RoutingChatModel(sp.GetRequiredService<IModelRouter>(), providers);
        });

        // Embeddings (Knowledge/RAG): a real OpenAI-compatible adapter when an endpoint is
        // configured (prefer a local Ollama/TEI URL), otherwise disabled (503 on use). Keyed on
        // the base URL's presence; the API key is optional (local backends need none).
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

    // Retry (exponential backoff + jitter) + circuit-breaker around provider calls, so a rate-limit
    // (429) or transient 5xx is retried and a failing provider trips the breaker instead of hammering
    // it. Deliberately NO aggressive attempt timeout — LLM calls (incl. streaming) legitimately run
    // for tens of seconds, so the standard handler's short timeouts would break them.
    private static void Resilience(ResiliencePipelineBuilder<HttpResponseMessage> builder) => builder
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions());

    // Wrap a cloud provider so PII is masked before the request leaves the office and restored in
    // the reply. Local providers skip this — their data never leaves the cluster.
    private static RedactingChatProvider Cloud(IServiceProvider sp, IChatProvider inner) =>
        new(inner, sp.GetRequiredService<IPiiRedactor>());

    private static OpenAiCompatibleChatProvider Compat(IServiceProvider sp, string name, string baseUrl, string? apiKeyEnvVar) =>
        new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
            sp.GetRequiredService<IEnvironment>(), name, baseUrl, apiKeyEnvVar);
}
