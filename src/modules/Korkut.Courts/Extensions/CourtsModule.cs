using Korkut.Courts.Services;
using Korkut.Courts.Repositories;
using Korkut.Contract.Courts;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Courts.Extensions;

/// <summary>Self-registration for the Courts module: the repository + the cross-module name lookup.</summary>
public sealed class CourtsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICourtRepository, CourtRepository>();
        services.AddScoped<ICourtLookup, CourtLookup>();
    }
}
