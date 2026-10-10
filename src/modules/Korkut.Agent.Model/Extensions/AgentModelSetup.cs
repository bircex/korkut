using Korkut.Agent.Model.Services;
using Korkut.Contract.Agent.Model;
using Korkut.Contract.Pii;
using Korkut.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace Korkut.Agent.Model.Extensions;

/// <summary>
/// Registers the Agent model layer: one chat provider, the model gateway (llmr), which every model
/// call goes through; the <see cref="IChatModelService"/> facade over it; and the cross-cutting
/// services (governance + ledger logging). Assumes the shared AI runtime is already registered
/// (<c>AddKorkutAi</c>: the "ai" <see cref="HttpClient"/>, <see cref="IEnvironment"/>,
/// <see cref="RuntimeConfig"/>, <see cref="IPiiRedactorService"/>) and that the host binds
/// <see cref="Korkut.Agent.Ledger.ILlmCallRepository"/> (it touches the Agent DB).
///
/// Redaction wraps the provider: the gateway may route to a cloud model, so office PII is masked
/// before any request leaves Korkut, whichever model the office has chosen.
/// </summary>
public static class AgentModelSetup
{
    public static IServiceCollection AddKorkutAgentModel(this IServiceCollection services)
    {
        services.AddScoped<IChatProviderService>(sp =>
        {
            var config = sp.GetRequiredService<RuntimeConfig>();
            return new RedactingChatProviderService(
                new OpenAiCompatibleChatProviderService(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai"),
                    sp.GetRequiredService<IEnvironment>(),
                    "llmr", config.LlmrUrl is { } url ? $"{url}/v1" : string.Empty, "KORKUT_LLMR_TOKEN"),
                sp.GetRequiredService<IPiiRedactorService>());
        });
        services.AddScoped<IChatModelService, GatewayChatModelService>();

        // Cross-cutting: per-office budget and concurrency, and the call ledger.
        services.AddSingleton<IOfficeConcurrencyGate, OfficeConcurrencyGate>();
        services.AddScoped<IUsageGovernorService, UsageGovernorService>();
        services.AddScoped<ILlmCallLogService, LlmCallLogService>();

        return services;
    }
}
