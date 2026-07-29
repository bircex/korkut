using Korkut.Correspondence.Services;
using Korkut.Correspondence.Repositories;
using Korkut.Contract.Correspondence;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Correspondence.Extensions;

/// <summary>Self-registration for the Correspondence module: binds the repository + service ports.</summary>
public sealed class CorrespondenceModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICorrespondenceRepository, CorrespondenceRepository>();
        services.AddScoped<ICorrespondenceService, CorrespondenceService>();
        services.AddScoped<Korkut.Contract.Correspondence.ICorrespondenceLookup, CorrespondenceLookup>();
    }
}
