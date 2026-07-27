using Korkut.Contract.Clients;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Clients;

/// <summary>
/// Self-registration for the Clients module: binds the repository + service ports. The entity
/// mapping (<see cref="ClientConfiguration"/>) is discovered by the shared context, and the
/// controller by MVC's application parts — the host wires both when it adds the module.
/// </summary>
public sealed class ClientsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientService, ClientService>();
    }
}
