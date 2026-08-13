namespace Korkut.Contract.Offices;

/// <summary>
/// A chat provider this build ships an adapter for. <see cref="Configured"/> reports whether its
/// API key is present in the environment — keys themselves are never surfaced, only their presence
/// — so the console can grey out a provider nobody can actually call yet.
/// </summary>
public sealed record AdminProviderResponse(string Name, string Label, bool Configured, bool IsLocal);

/// <summary>The write shape for a catalog entry (create or update, keyed by <see cref="Key"/>).</summary>
public sealed record AdminModelUpsertRequest(
    string Key, string Label, string Provider, string ModelId, string? BaseUrl, int? MaxTokens,
    bool IsLocal, bool Enabled, int SortOrder);

/// <summary>
/// The system-wide default model: what every office WITHOUT its own enabled assignment resolves to
/// — the floor of the whole routing chain (see
/// <see cref="Korkut.Contract.Agent.Model.IOfficeModelOverrideService"/>). There is no tier beneath
/// it, so a HEALTHY system always has a non-null <see cref="ModelKey"/>; null only appears
/// transiently (a fresh install before the first-boot bootstrap runs, or every provider's key was
/// removed after it was set) and the console flags that state rather than silently guessing a vendor.
/// </summary>
public sealed record AdminPlatformDefaultResponse(
    string? ModelKey, string? Label, string? Provider, string? ModelId, DateTimeOffset? UpdatedAt);

/// <summary>
/// The admin console's model-plane surface: the DB-backed catalog (replacing the operator's
/// read-only KORKUT_MODELS_FILE), the provider roster, and the one-switch system default. Every
/// write takes effect at runtime — no redeploy, no restart — because the chat router re-reads this
/// state through <see cref="Korkut.Contract.Agent.Model.IOfficeModelOverrideService"/> on a short cache.
/// </summary>
public interface IModelCatalogService
{
    /// <summary>Every catalog entry, ordered for display (sort order, then label).</summary>
    Task<Result<IReadOnlyList<AdminModelResponse>>> ListAsync(CancellationToken ct = default);

    /// <summary>Create or update the entry with this key. The provider must be one this build knows.</summary>
    Task<Result<AdminModelResponse>> UpsertAsync(AdminModelUpsertRequest request, CancellationToken ct = default);

    /// <summary>Remove a catalog entry. Refused while it is the system default or assigned to an office.</summary>
    Task<Result> DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>The providers this build can route to, and whether each one's key is configured.</summary>
    Task<Result<IReadOnlyList<AdminProviderResponse>>> ProvidersAsync(CancellationToken ct = default);

    Task<Result<AdminPlatformDefaultResponse>> GetDefaultAsync(CancellationToken ct = default);

    /// <summary>Repoint the system default at this catalog key. There is deliberately no way to
    /// unset it (an empty/unknown key is rejected) — it is always REPLACED, never cleared, because
    /// every office without its own pin depends on it resolving to something real.</summary>
    Task<Result<AdminPlatformDefaultResponse>> SetDefaultAsync(string modelKey, CancellationToken ct = default);
}
