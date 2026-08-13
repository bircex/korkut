using Korkut.Contract.Agent.Model;
using Korkut.Core;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// Resolves ONLY the local/on-prem leg of routing (<see cref="ModelRouting"/>: specialty → tier +
/// the local-availability probe). A "cloud" tier — or a "local" tier with nothing reachable and
/// fallback enabled — resolves to null, meaning the caller (<see cref="RoutingChatModelService"/>)
/// falls through to the dynamic system default. Vendor choice is never decided here.
/// </summary>
internal sealed class ModelRouterService(ModelRouting routing, IEnvironment env, ILocalModelProbe local) : IModelRouterService
{
    /// <summary>The local backend has no per-provider RuntimeConfig max; a sane, operator-tunable cap.</summary>
    private const int LocalMaxTokens = 4096;

    public ModelSelection? ResolveLocal(string specialty)
    {
        var tier = routing.ResolveTier(specialty, local.IsAvailable);
        if (tier != "local")
        {
            return null;
        }

        // Local (Ollama) model id is operator-set; kept off the frozen RuntimeConfig contract.
        return new ModelSelection(ChatProviders.Ollama, env.Get("KORKUT_LOCAL_MODEL") ?? "llama3.1", LocalMaxTokens);
    }
}

/// <summary>Default probe: no local backend until the GPU/Ollama host is wired (always false).</summary>
internal sealed class LocalModelUnavailable : ILocalModelProbe
{
    public bool IsAvailable => false;
}
