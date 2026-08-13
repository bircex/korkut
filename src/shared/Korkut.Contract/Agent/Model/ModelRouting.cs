namespace Korkut.Contract.Agent.Model;

/// <summary>
/// The specialty → tier rule (ported from <c>config/model-routing.yaml</c>, KDM-033): which task
/// intents (dispatcher/hearing/petition/research) prefer the LOCAL backend for privacy/cost reasons,
/// independent of which cloud vendor is in use. This is the only remaining "routing" decision in the
/// system — WHICH cloud provider serves a "cloud" tier request is no longer a compile-time choice
/// baked in here; it is the dynamic system default (see IOfficeModelOverrideService), resolved fresh
/// on every request from the admin console's configuration.
/// </summary>
public sealed class ModelRouting(
    IReadOnlyDictionary<string, string> specialties,
    bool fallbackToCloudIfNoLocal,
    string defaultTier = "cloud")
{
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
}
