using System;
using System.Collections.Generic;
using System.Linq;
using Korkut.Agent.Brain.Extensions;
using Korkut.Agent.Chat;
using Korkut.Agent.Chat.Extensions;
using Korkut.Agent.Drafting;
using Korkut.Agent.Drafting.Extensions;
using Korkut.Agent.Ledger;
using Korkut.Agent.Model.Extensions;
using Korkut.Contract.Agent;
using Korkut.Infrastructure.Agent.Persistence;
using Korkut.Infrastructure.Chat;
using Korkut.Tools;
using Korkut.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Agent.Host.Setup;

/// <summary>
/// Wires the agent vertical on top of the operational stack (AddKorkutDomain) + the Agent DB
/// (AddKorkutAgentPersistence) + the shared AI runtime (AddKorkutAi): the model/brain/chat/
/// drafting layers, the Agent-DB persistence adapters, the tool registry, and the agent caller.
/// </summary>
public static class AgentHostSetup
{
    public static IServiceCollection AddKorkutAgentHost(this IServiceCollection services)
    {
        // Agent layers (each module owns its own registration).
        services.AddKorkutAgentModel();
        services.AddKorkutAgentBrain();
        services.AddKorkutAgentChat();
        services.AddKorkutAgentDrafting();

        // Agent-DB persistence adapters (bind AgentDbContext / the operational conversation store).
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IDraftDocumentRepository, DraftDocumentRepository>();
        services.AddScoped<ILlmCallRepository, LlmCallRepository>();
        services.AddScoped<IAgentUnitOfWork, AgentUnitOfWork>();

        // Tool registry: every IAgentTool in Korkut.Tools, plus the catalog that gates them.
        services.AddScoped<IToolCatalog, ToolCatalog>();
        foreach (var tool in typeof(ToolCatalog).Assembly.GetTypes()
                     .Where(t => typeof(IAgentTool).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }))
        {
            services.AddScoped(typeof(IAgentTool), tool);
        }

        // The agent app services resolve the author display name through the operational users table.
        services.AddScoped<Korkut.Core.IUserLookupService, Korkut.Infrastructure.Persistence.Repositories.UserLookupService>();

        // The agent's caller identity has the same shape as the operational one; adapt it.
        services.AddScoped<ICurrentUser, AgentCurrentUser>();
        return services;
    }
}

/// <summary>Adapts the operational <see cref="Korkut.Core.ICurrentUser"/> to the agent's caller port.</summary>
internal sealed class AgentCurrentUser(Korkut.Core.ICurrentUser core) : ICurrentUser
{
    public Guid OfficeId => core.OfficeId;

    public Guid UserId => core.UserId;

    public IReadOnlySet<string> Permissions => core.Permissions;
}
