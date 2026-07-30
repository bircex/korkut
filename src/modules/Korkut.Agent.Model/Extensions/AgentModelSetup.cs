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
        // Inspect the env once at startup to decide which cloud provider backs the routing.
        var config = new RuntimeConfig(new SystemEnvironment());

        // Routing (specialty → tier → provider). The cloud tier uses DeepSeek when a key is set,
        // otherwise the always-on Anthropic. hearing stays local and falls back to cloud until the
        // GPU/Ollama host is wired. This is the single place the model wiring is decided.
        var cloudProvider = config.HasDeepSeekKey ? "deepseek" : "claude";
        services.AddSingleton(new ModelRouting(
            backends: new Dictionary<string, string> { ["cloud"] = cloudProvider, ["local"] = "ollama" },
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
        services.AddScoped<IChatProviderService>(sp => new RedactingChatProviderService(
            ActivatorUtilities.CreateInstance<AnthropicChatProviderService>(sp),
            sp.GetRequiredService<IPiiRedactorService>()));

        // DeepSeek (OpenAI-compatible) — registered when DEEPSEEK_API_KEY is present.
        if (config.HasDeepSeekKey)
        {
            services.AddScoped<IChatProviderService>(sp => new RedactingChatProviderService(
                new OpenAiCompatibleChatProviderService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
                    sp.GetRequiredService<IEnvironment>(),
                    "deepseek", config.DeepSeekBaseUrl, "DEEPSEEK_API_KEY"),
                sp.GetRequiredService<IPiiRedactorService>()));
        }

        // The routing facade the reasoning layer calls.
        services.AddScoped<IChatModelService, RoutingChatModelService>();

        // Cross-cutting (see the decorator note above).
        services.AddSingleton<IOfficeConcurrencyGate, OfficeConcurrencyGate>();
        services.AddScoped<IUsageGovernorService, UsageGovernorService>();
        services.AddScoped<ILlmCallLogService, LlmCallLogService>();

        return services;
    }
}
