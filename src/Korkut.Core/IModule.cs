using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Core;

/// <summary>
/// A self-contained module of the monolith. Each module wires ITSELF up — its services,
/// repositories and any minimal-API routes — so the host just discovers the modules and calls
/// them, never reaching into a module's internals. Controllers are picked up automatically once
/// the host adds the module's assembly as an application part; <see cref="MapEndpoints"/> is only
/// for modules that also expose minimal-API routes.
/// </summary>
public interface IModule
{
    /// <summary>Register this module's DI services (and EF configurations) against the container.</summary>
    void Register(IServiceCollection services, IConfiguration configuration);

    /// <summary>Map this module's minimal-API routes, if any. Default: nothing (controllers only).</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
