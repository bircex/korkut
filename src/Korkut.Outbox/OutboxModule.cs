using Korkut.Contract.Outbox;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Outbox;

/// <summary>Self-registration for the Outbox module: binds the repository + service ports.</summary>
public sealed class OutboxModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IOutboxService, OutboxService>();
    }
}
