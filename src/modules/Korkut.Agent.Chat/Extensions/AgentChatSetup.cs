using Korkut.Agent.Chat.Services;
using Korkut.Agent.Chat.Abstractions;
using Korkut.Contract.Agent.Chat;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Agent.Chat.Extensions;

/// <summary>
/// Registers the Chat use-case: session + chat orchestration. Assumes the Brain
/// (IAgentToolLoopService), Model (governance/ledger), Settings repos, Knowledge search, the
/// text extractor, and the session store (ISessionRepository) are registered.
/// </summary>
public static class AgentChatSetup
{
    public static IServiceCollection AddKorkutAgentChat(this IServiceCollection services)
    {
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ISessionChatService, SessionChatService>();
        services.AddScoped<ISessionAppService, SessionAppService>();
        services.AddScoped<IChatAppService, ChatAppService>();
        services.AddScoped<Korkut.Agent.Chat.Repositories.ISessionRepository, Korkut.Agent.Chat.Repositories.SessionRepository>();
        return services;
    }
}
