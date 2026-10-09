using Korkut.Agent.Drafting.Repositories;
using Korkut.Agent.Chat.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using Korkut.Agent.Brain.Extensions;
using Korkut.Contract.Agent.Chat;
using Korkut.Agent.Chat.Extensions;
using Korkut.Contract.Agent.Drafting;
using Korkut.Agent.Drafting.Extensions;
using Korkut.Agent.Ledger;
using Korkut.Agent.Model.Extensions;
using Korkut.Contract.Agent;
using Korkut.Agent.Persistence;
using Korkut.Tools;
using Korkut.Tools.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Api.Setup;

/// <summary>
/// Wires the agent vertical on top of the operational stack (AddKorkutDomain) + the Agent DB
/// (AddKorkutAgentPersistence) + the shared AI runtime (AddKorkutAi) + the Knowledge module
/// (AddKorkutKnowledge, which supplies the retrieval chat is grounded on): the model/brain/chat/
/// drafting layers, the Agent-DB persistence adapters, the tool registry, and the agent caller.
/// </summary>
public static class AgentSetup
{
    public static IServiceCollection AddKorkutAgent(this IServiceCollection services)
    {
        // Agent layers (each module owns its own registration).
        services.AddKorkutAgentModel();
        services.AddKorkutAgentBrain();
        services.AddKorkutAgentChat();
        services.AddKorkutAgentDrafting();

        // Agent-DB persistence adapters (bind AgentDbContext / the operational conversation store).
        services.AddScoped<IDraftDocumentRepository, DraftDocumentRepository>();
        services.AddScoped<ILlmCallRepository, LlmCallRepository>();
        services.AddScoped<IAgentUnitOfWork, AgentUnitOfWork>();

        // Tool registry: every IAgentTool in Korkut.Tools, plus the catalog that gates them. Their
        // dependencies (ILawClientsSearchService/ILawClientsDocumentService, ICalculatorService) are
        // registered by the LawClients/Calculators modules via AddKorkutDomain, so a tool resolves
        // them the same way any Contract service is consumed.
        services.AddScoped<IToolCatalog, ToolCatalog>();
        foreach (var tool in typeof(ToolCatalog).Assembly.GetTypes()
                     .Where(t => typeof(IAgentTool).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }))
        {
            services.AddScoped(typeof(IAgentTool), tool);
        }

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
