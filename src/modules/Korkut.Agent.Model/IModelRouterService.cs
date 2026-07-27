
namespace Korkut.Agent.Model;

/// <summary>
/// Maps a task specialty to a concrete provider + model, honouring <c>model-routing.yaml</c>
/// (specialty → tier → provider) and the local-availability fallback. Given the set of
/// providers actually registered, it never returns one that isn't wired up.
/// </summary>
public interface IModelRouterService
{
    ModelSelection Resolve(string specialty, IReadOnlySet<string> availableProviders);
}

/// <summary>Whether the local (on-prem) model backend is currently reachable. Drives the cloud fallback.</summary>
public interface ILocalModelProbe
{
    bool IsAvailable { get; }
}
