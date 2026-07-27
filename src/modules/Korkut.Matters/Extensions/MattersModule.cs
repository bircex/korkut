using Korkut.Matters.Services;
using Korkut.Matters.Repositories;
using Korkut.Contract.Matters;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Matters.Extensions;

/// <summary>Self-registration for the Matters module: repository, service, and the cross-module lookup.</summary>
public sealed class MattersModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IMatterRepository, MatterRepository>();
        services.AddScoped<IMatterService, MatterService>();
        services.AddScoped<IMatterLookup, MatterLookup>();
    }
}
