using Korkut.Agent.Model.Services;
using Korkut.Contract.Agent.Model;
using Korkut.Contract.Pii;
using Korkut.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http;

namespace Korkut.Agent.Model.Extensions;

/// <summary>
/// Registers the Agent model layer: the specialty router, the chat providers (cloud PII-redacted),
/// the routing <see cref="IChatModelService"/> facade, and the cross-cutting services
/// (governance + ledger logging). Assumes the shared AI runtime is already registered
/// (<c>AddKorkutAi</c>: the "ai" <see cref="System.Net.Http.HttpClient"/>, <see cref="IEnvironment"/>,
/// <see cref="RuntimeConfig"/>, <see cref="IPiiRedactorService"/>) and that the host binds
/// <see cref="Korkut.Agent.Ledger.ILlmCallRepository"/> (it touches the Agent DB).
///
/// The **decorator chain** over the model is the point of this layer. Redaction wraps each CLOUD
/// provider here. Governance + ledger-logging become transparent <see cref="IChatModelService"/>
/// decorators when the reasoning layer (Brain) stops calling them explicitly; until then they are
/// registered as the services the reasoning layer resolves. Target order:
/// <code>Governance → Redaction → Ledger-log → Router → Provider</code>
/// </summary>
public static class AgentModelSetup
{
    public static IServiceCollection AddKorkutAgentModel(this IServiceCollection services)
    {
        // Inspect the env once at startup only for provider transport config (base URLs) — never to
        // pick a preferred VENDOR. That choice is 100% dynamic admin config from here on (see
        // IOfficeModelOverrideService); nothing in this method encodes "deepseek" or "claude" as a
        // fallback favourite.
        var config = new RuntimeConfig(new SystemEnvironment());

        // The only routing decision left at this layer: does a specialty prefer the LOCAL backend
        // (hearing, for on-prem/cost reasons), independent of which cloud vendor is configured.
        // hearing falls back to cloud until the GPU/Ollama host is wired.
        services.AddSingleton(new ModelRouting(
            specialties: new Dictionary<string, string>
            {
                ["dispatcher"] = "cloud",
                ["hearing"] = "local",
                ["petition"] = "cloud",
                ["research"] = "cloud",
            },
            fallbackToCloudIfNoLocal: true));
        services.AddSingleton<ILocalModelProbe, LocalModelUnavailable>();
        services.AddScoped<IModelRouterService, ModelRouterService>();

        // Providers. Every cloud provider is wrapped with the PII-redaction decorator so raw office
        // data never reaches the cloud; local providers (Ollama/vLLM) would not need it. Providers
        // are keyed by Name inside RoutingChatModelService.
        //
        // Registration is UNCONDITIONAL — deliberately. Model choice is now runtime admin config
        // (a catalog + a platform default, both in the DB), so gating the adapter on its key being
        // present at BOOT would mean an operator who adds a key can still never select that provider
        // until the process restarts, which is exactly the coupling the dynamic model plane removes.
        // A provider whose key is missing fails its own call with a clear message
        // ("<provider> API anahtarı yapılandırılmamış."), and the admin console greys it out up front
        // via ChatProviders + RuntimeConfig key presence, so an admin never picks a dead one blind.
        services.AddScoped<IChatProviderService>(sp => new RedactingChatProviderService(
            ActivatorUtilities.CreateInstance<AnthropicChatProviderService>(sp),
            sp.GetRequiredService<IPiiRedactorService>()));

        // The OpenAI-compatible family — one adapter, four endpoints.
        OpenAiCompatible(ChatProviders.DeepSeek, config.DeepSeekBaseUrl, "DEEPSEEK_API_KEY");
        OpenAiCompatible(ChatProviders.Gemini, config.GeminiBaseUrl, "GEMINI_API_KEY");
        OpenAiCompatible(ChatProviders.OpenAi, config.OpenAiBaseUrl, "OPENAI_API_KEY");
        OpenAiCompatible(ChatProviders.Groq, config.GroqBaseUrl, "GROQ_API_KEY");

        // Local backend (Ollama/vLLM): no key, and no PII redaction needed — office data never
        // leaves the cluster. Only registered when the operator points KORKUT_OLLAMA_URL somewhere.
        if (config.OllamaBaseUrl is { } ollamaUrl)
        {
            services.AddScoped<IChatProviderService>(sp => new OpenAiCompatibleChatProviderService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
                sp.GetRequiredService<IEnvironment>(),
                ChatProviders.Ollama, ollamaUrl, apiKeyEnvVar: null));
        }

        void OpenAiCompatible(string name, string baseUrl, string apiKeyEnvVar) =>
            services.AddScoped<IChatProviderService>(sp => new RedactingChatProviderService(
                new OpenAiCompatibleChatProviderService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
                    sp.GetRequiredService<IEnvironment>(),
                    name, baseUrl, apiKeyEnvVar),
                sp.GetRequiredService<IPiiRedactorService>()));

        // The routing facade the reasoning layer calls.
        services.AddScoped<IChatModelService, RoutingChatModelService>();

        // Cross-cutting (see the decorator note above).
        services.AddSingleton<IOfficeConcurrencyGate, OfficeConcurrencyGate>();
        services.AddScoped<IUsageGovernorService, UsageGovernorService>();
        services.AddScoped<ILlmCallLogService, LlmCallLogService>();

        return services;
    }
}
