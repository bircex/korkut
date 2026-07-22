using Korkut.Agent.Application.Ai;
using Korkut.Agent.Application.Abstractions.Ai;
using Korkut.Agent.Application.Abstractions.Persistence;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Agent.Infrastructure.Ai.Providers;
using Korkut.Agent.Infrastructure.Ai.Routing;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Runtime;
using Korkut.Pii;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Korkut.Agent.Setup;

/// <summary>
/// Wires the Agent host's swappable chat runtime: the model-routing rules, the per-provider chat
/// adapters (registered only when their key is present; cloud providers wrapped so raw PII is masked
/// before it leaves the office), and the routing facade consumers depend on (<see cref="IChatModel"/>).
/// If no provider is configured a disabled model reports 503 and the host still boots. The shared
/// runtime (IEnvironment / RuntimeConfig / IPiiRedactor / the "ai" HttpClient) comes from
/// <c>AddKorkutAi</c>; adding a provider is one registration here + config, no consumer changes.
/// </summary>
public static class ChatSetup
{
    public static IServiceCollection AddKorkutChat(this IServiceCollection services)
    {
        // Startup-time inspection of the env to decide which providers to register (keys are set at
        // boot). The DI IEnvironment/RuntimeConfig from AddKorkutAi are what run at request time.
        var env = new SystemEnvironment();
        var config = new RuntimeConfig(env);

        services.AddSingleton(ModelRoutingLoader.LoadOrDefault(env));
        services.AddSingleton<ILocalModelProbe, LocalModelUnavailable>();
        services.AddSingleton<IModelRouter, ModelRouter>();

        // Chat providers, keyed by their model-routing.yaml token, registered only when configured
        // so the router never resolves an unwired provider. Cloud providers are PII-masked; local
        // providers are not (their data never leaves the cluster).
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

        return services;
    }

    // Retry (exponential backoff + jitter) + circuit-breaker around provider calls. No aggressive
    // attempt timeout — LLM calls (incl. streaming) legitimately run for tens of seconds.
    private static void Resilience(ResiliencePipelineBuilder<HttpResponseMessage> builder) => builder
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions());

    private static RedactingChatProvider Cloud(IServiceProvider sp, IChatProvider inner) =>
        new(inner, sp.GetRequiredService<IPiiRedactor>());

    private static OpenAiCompatibleChatProvider Compat(IServiceProvider sp, string name, string baseUrl, string? apiKeyEnvVar) =>
        new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
            sp.GetRequiredService<IEnvironment>(), name, baseUrl, apiKeyEnvVar);
}
