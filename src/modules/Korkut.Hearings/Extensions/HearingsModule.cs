using Korkut.Hearings.Services;
using Korkut.Hearings.Repositories;
using Korkut.Contract.Hearings;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Hearings.Extensions;

/// <summary>Self-registration for the Hearings module: repository, service, and the cross-module lookup.</summary>
public sealed class HearingsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IHearingRepository, HearingRepository>();
        services.AddScoped<IHearingService, HearingService>();
        services.AddScoped<IHearingLookup, HearingLookup>();
    }
}
