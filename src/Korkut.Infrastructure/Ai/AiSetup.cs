using Korkut.Application.Ai;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // --- Chat providers, keyed by their model-routing.yaml token, registered only when
        //     configured so the router never resolves an unwired provider. ---
        services.AddHttpClient("ai");

        if (config.HasAnthropicKey)
        {
            services.AddHttpClient<AnthropicChatProvider>();
            services.AddTransient<IChatProvider>(sp => sp.GetRequiredService<AnthropicChatProvider>());
        }

        if (config.HasOpenAiKey)
        {
            services.AddTransient<IChatProvider>(sp => Compat(sp, "gpt", "https://api.openai.com/v1", "OPENAI_API_KEY"));
        }

        if (config.HasGroqKey)
        {
            services.AddTransient<IChatProvider>(sp => Compat(sp, "groq", "https://api.groq.com/openai/v1", "GROQ_API_KEY"));
        }

        // Local (Ollama/vLLM) only when the operator opts in with a base URL.
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

        // Embeddings: the real adapter lands with the Knowledge service; disabled until then.
        services.AddScoped<IEmbedder, DisabledEmbedder>();

        return services;
    }

    private static OpenAiCompatibleChatProvider Compat(IServiceProvider sp, string name, string baseUrl, string? apiKeyEnvVar) =>
        new(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
            sp.GetRequiredService<IEnvironment>(), name, baseUrl, apiKeyEnvVar);
}
