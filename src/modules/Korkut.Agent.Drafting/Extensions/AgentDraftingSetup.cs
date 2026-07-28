using Korkut.Agent.Drafting.Services;
using Korkut.Agent.Drafting.Abstractions;
using Korkut.Contract.Agent.Drafting;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Agent.Drafting.Extensions;

/// <summary>
/// Registers the Drafting use-case: generate a draft via the model, persist versions, export.
/// Assumes the Model (IChatModelService), the draft store (IDraftDocumentRepository), the session
/// service (ISessionService), the exporter (IDocumentExporter) and the template catalog are
/// registered.
/// </summary>
public static class AgentDraftingSetup
{
    public static IServiceCollection AddKorkutAgentDrafting(this IServiceCollection services)
    {
        services.AddScoped<IDraftDocumentService, DraftDocumentService>();
        services.AddScoped<IDraftingService, DraftingService>();
        services.AddScoped<IDocumentAppService, DocumentAppService>();
        return services;
    }
}
