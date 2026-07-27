using Korkut.Contract.Deadlines;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Deadlines;

/// <summary>Self-registration for the Deadlines module: binds the repository + service ports.</summary>
public sealed class DeadlinesModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDeadlineRepository, DeadlineRepository>();
        services.AddScoped<IDeadlineService, DeadlineService>();
    }
}
