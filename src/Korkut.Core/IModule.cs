using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Core;

/// <summary>
/// A self-contained module of the monolith. Each module wires ITSELF up — its services,
/// repositories and EF configurations — so the host just discovers the modules and calls them,
/// never reaching into a module's internals. The host adds each module's assembly as an MVC
/// application part, so the module's controllers are picked up automatically.
/// </summary>
public interface IModule
{
    /// <summary>Register this module's DI services (and its EF entity configurations) on the container.</summary>
    void Register(IServiceCollection services, IConfiguration configuration);
}
