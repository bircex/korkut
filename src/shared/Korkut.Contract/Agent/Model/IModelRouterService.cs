
namespace Korkut.Contract.Agent.Model;

/// <summary>
/// The LOCAL half of routing: does this specialty prefer the on-prem backend, and is one actually
/// reachable right now? This is the only specialty-driven decision left — vendor choice for
/// everything else is the dynamic system default (see IOfficeModelOverrideService), not a
/// specialty-to-provider table.
/// </summary>
public interface IModelRouterService
{
    /// <summary>Null when this specialty should use the system default (a "cloud" tier, or a "local"
    /// tier with no local backend reachable and fallback enabled) — never a hardcoded vendor guess.</summary>
    ModelSelection? ResolveLocal(string specialty);
}

/// <summary>Whether the local (on-prem) model backend is currently reachable. Drives the cloud fallback.</summary>
public interface ILocalModelProbe
{
    bool IsAvailable { get; }
}
