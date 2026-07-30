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

        // RAG grounding: the retrieval adapter that calls the internal Knowledge /search over HTTP
        // (forwarding the caller's token), degrading to ungrounded when Knowledge is unconfigured.
        services.AddHttpContextAccessor();
        services.AddScoped<Korkut.Knowledge.IKnowledgeSearchService, HttpKnowledgeSearch>();

        // Agent-DB persistence adapters (bind AgentDbContext / the operational conversation store).
        services.AddScoped<IDraftDocumentRepository, DraftDocumentRepository>();
        services.AddScoped<ILlmCallRepository, LlmCallRepository>();
        services.AddScoped<IAgentUnitOfWork, AgentUnitOfWork>();

        // Law/source clients the legal tools depend on: every concrete *Client whose constructor
        // needs only injectable deps (IHttpClientFactory / IEnvironment / another *Client) is
        // self-registered, and the ISourceClient ones are also exposed as the IEnumerable<ISourceClient>
        // the fetch/search tools iterate. Parameterized clients (e.g. BedestenClient, one instance per
        // court source) are skipped here — they need their per-source config registered explicitly.
        static bool Injectable(Type p) =>
            p == typeof(System.Net.Http.IHttpClientFactory) || p == typeof(Korkut.Core.IEnvironment)
            || p.Name.EndsWith("Client", StringComparison.Ordinal);
        foreach (var clientType in typeof(Korkut.Tools.Law.ISourceClient).Assembly.GetTypes()
                     .Where(t => t is { IsAbstract: false, IsInterface: false } && t.Name.EndsWith("Client"))
                     .Where(t => t.GetConstructors().FirstOrDefault()?.GetParameters().All(pi => Injectable(pi.ParameterType)) == true))
        {
            services.AddScoped(clientType);
            if (typeof(Korkut.Tools.Law.ISourceClient).IsAssignableFrom(clientType))
            {
                services.AddScoped(typeof(Korkut.Tools.Law.ISourceClient), sp => sp.GetRequiredService(clientType));
            }
        }

        // Tool registry: every IAgentTool in Korkut.Tools, plus the catalog that gates them.
        services.AddScoped<IToolCatalog, ToolCatalog>();
        foreach (var tool in typeof(ToolCatalog).Assembly.GetTypes()
                     .Where(t => typeof(IAgentTool).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }))
        {
            services.AddScoped(typeof(IAgentTool), tool);
        }

        // The agent app services resolve the author display name through the operational users table.

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
