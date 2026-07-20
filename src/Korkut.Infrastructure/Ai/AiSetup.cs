using Korkut.Application.Ai;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// Wires the assistant/LLM runtime: the environment adapter + ported RuntimeConfig, and
/// the chat-completion client. The real Anthropic client is used when
/// <c>ANTHROPIC_API_KEY</c> is present; otherwise a disabled client that reports
/// "unavailable" (503) so the API still boots without a provider key. Tests swap in a
/// deterministic fake via ConfigureTestServices.
/// </summary>
public static class AiSetup
{
    public static IServiceCollection AddKorkutAi(this IServiceCollection services)
    {
        services.TryAddSingleton<IEnvironment, SystemEnvironment>();
        services.TryAddSingleton<RuntimeConfig>();
        services.AddHttpClient<AnthropicLlmClient>();

        services.AddScoped<ILlmClient>(sp =>
            sp.GetRequiredService<RuntimeConfig>().HasAnthropicKey
                ? sp.GetRequiredService<AnthropicLlmClient>()
                : new DisabledLlmClient());

        return services;
    }
}
