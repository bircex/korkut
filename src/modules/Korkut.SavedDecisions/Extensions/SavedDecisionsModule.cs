using Korkut.Contract.SavedDecisions;
using Korkut.Core;
using Korkut.SavedDecisions.Repositories;
using Korkut.SavedDecisions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.SavedDecisions.Extensions;

/// <summary>Self-registration for the SavedDecisions module: the repository + the service.</summary>
public sealed class SavedDecisionsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISavedDecisionRepository, SavedDecisionRepository>();
        services.AddScoped<ISavedDecisionService, SavedDecisionService>();
    }
}
