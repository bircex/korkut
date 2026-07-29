using Korkut.Agent.Model.Services;
using Korkut.Contract.Agent.Model;
using Korkut.Contract.Pii;
using Korkut.Core;
using Microsoft.Extensions.DependencyInjection;

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
        // Routing.
        services.AddSingleton<ILocalModelProbe, LocalModelUnavailable>();
        services.AddScoped<IModelRouterService, ModelRouterService>();

        // Providers. The cloud provider (claude) is wrapped with the PII-redaction decorator so raw
        // office data never reaches the cloud; local providers (Ollama/vLLM, added by the host from
        // RuntimeConfig) do not need it. Providers are keyed by Name inside RoutingChatModelService.
        services.AddScoped<IChatProviderService>(sp => new RedactingChatProviderService(
            ActivatorUtilities.CreateInstance<AnthropicChatProviderService>(sp),
            sp.GetRequiredService<IPiiRedactorService>()));
        // OpenAI-compatible local/cloud providers are registered by the host per configured backend
        // (each with its own name + base URL), e.g.
        //   services.AddScoped<IChatProviderService>(sp => new OpenAiCompatibleChatProviderService("groq", ...));

        // The routing facade the reasoning layer calls.
        services.AddScoped<IChatModelService, RoutingChatModelService>();

        // Cross-cutting (see the decorator note above).
        services.AddSingleton<IOfficeConcurrencyGate, OfficeConcurrencyGate>();
        services.AddScoped<IUsageGovernorService, UsageGovernorService>();
        services.AddScoped<ILlmCallLogService, LlmCallLogService>();

        return services;
    }
}
