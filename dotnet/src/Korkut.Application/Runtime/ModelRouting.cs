namespace Korkut.Application.Runtime;

/// <summary>
/// Ported model-routing rules from <c>config/model-routing.yaml</c> (KDM-033),
/// preserving the Python router's semantics exactly:
/// <list type="bullet">
/// <item>each <b>tier</b> (cloud|local) maps to a provider name (swappable);</item>
/// <item>a <b>specialty</b> resolves to a tier, defaulting to <see cref="DefaultTier"/>
/// (<c>cloud</c>) when unlisted;</item>
/// <item>if a <c>local</c> tier is requested but the local model is unavailable, fall
/// back to <c>cloud</c> when <see cref="FallbackToCloudIfNoLocal"/>, else fail-fast.</item>
/// </list>
/// </summary>
public sealed class ModelRouting(
    IReadOnlyDictionary<string, string> backends,
    IReadOnlyDictionary<string, string> specialties,
    bool fallbackToCloudIfNoLocal,
    string defaultTier = "cloud")
{
    public IReadOnlyDictionary<string, string> Backends => backends;

    public IReadOnlyDictionary<string, string> Specialties => specialties;

    public bool FallbackToCloudIfNoLocal => fallbackToCloudIfNoLocal;

    public string DefaultTier => defaultTier;

    /// <summary>
    /// Resolve a specialty to a concrete tier. Mirrors Python <c>_resolve_tier</c>:
    /// an unlisted specialty uses the default tier; a local tier with no local model
    /// falls back to cloud only when configured, otherwise throws.
    /// </summary>
    public string ResolveTier(string specialty, bool localAvailable)
    {
        var tier = specialties.TryGetValue(specialty, out var t) ? t : defaultTier;
        if (tier == "local" && !localAvailable)
        {
            if (!fallbackToCloudIfNoLocal)
            {
                throw new InvalidOperationException(
                    $"local model unavailable for '{specialty}' and fallback disabled");
            }

            return "cloud";
        }

        return tier;
    }

    /// <summary>
    /// The provider name backing a tier. Mirrors Python <c>_provider_for</c>: an
    /// unmapped tier, or one whose provider isn't registered, is a hard error.
    /// </summary>
    public string ProviderForTier(string tier, IReadOnlySet<string> registeredProviders)
    {
        if (!backends.TryGetValue(tier, out var name) || string.IsNullOrEmpty(name) || !registeredProviders.Contains(name))
        {
            throw new InvalidOperationException(
                $"no provider configured for tier '{tier}' (backend={name ?? "null"})");
        }

        return name;
    }
}
