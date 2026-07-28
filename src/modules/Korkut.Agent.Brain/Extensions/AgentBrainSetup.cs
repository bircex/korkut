using Korkut.Agent.Brain.Services;
using Korkut.Agent.Brain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Agent.Brain.Extensions;

/// <summary>
/// Registers the Agent reasoning layer: the model&lt;-&gt;tools loop. Assumes the Model layer
/// (<c>AddKorkutAgentModel</c>: IChatModelService) and the Tools layer (IToolCatalog) are
/// already registered.
/// </summary>
public static class AgentBrainSetup
{
    public static IServiceCollection AddKorkutAgentBrain(this IServiceCollection services)
    {
        services.AddScoped<IAgentToolLoopService, ToolCallingLoopService>();
        return services;
    }
}
