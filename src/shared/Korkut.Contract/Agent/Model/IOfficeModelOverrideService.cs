namespace Korkut.Contract.Agent.Model;

/// <summary>Where a resolved model came from — the console shows it, and it keeps the precedence
/// chain diagnosable from a log line.</summary>
public static class ModelSelectionSource
{
    /// <summary>The office's own enabled assignment (admin pinned this office to a model).</summary>
    public const string Office = "office";

    /// <summary>The system-wide default every office without its own pin uses. The floor of the
    /// chain — there is no lower tier, no hardcoded vendor guess beneath it.</summary>
    public const string SystemDefault = "system_default";

    /// <summary>Nothing usable is configured (no office pin, and the system default is unset or
    /// points at a disabled/missing model) — a misconfiguration, not a supported steady state.
    /// Never returned as a live routing decision (that path throws instead); the admin console
    /// uses it to flag "this needs your attention" rather than silently guessing a provider.</summary>
    public const string Unconfigured = "unconfigured";
}

/// <summary>A resolved chat-model selection: which registered <see cref="IChatProviderService"/>
/// (by <see cref="IChatProviderService.Name"/>) to use, which model id to send it, and which layer
/// of the precedence chain decided (<see cref="ModelSelectionSource"/>).</summary>
public sealed record OfficeModelOverride(string Provider, string ModelId, int? MaxTokens, string Source, string ModelKey);

/// <summary>
/// The router's seam into the admin console's dynamic model configuration. Implemented by the
/// Organization module — already wired into every host via AddKorkutDomain, so no new project
/// reference is needed here — and consumed by the routing <see cref="IChatModelService"/>. Reads
/// come from a short cache so an admin's change reaches every replica without a restart.
///
/// The two methods are split (rather than one combined "office ?? default" call) because the caller
/// interleaves a THIRD concern between them — a specialty that prefers the local backend (see
/// <see cref="IModelRouterService"/>) wins over the system default but never over an office's own
/// pin. Full precedence, as <c>RoutingChatModelService</c> applies it:
/// <code>
/// 1. the office's own enabled assignment     (any specialty — an admin's pin is absolute)
/// 2. the local backend, if this specialty wants it and one is reachable
/// 3. the system-wide default model
/// 4. throw — there is no further fallback; nothing routes to a provider nobody chose
/// </code>
/// There is deliberately NO tier below the system default: an admin ALWAYS has an explicit,
/// dashboard-editable choice controlling every office that isn't individually pinned — never a
/// hardcoded "whichever provider happens to have a key" guess baked into the routing code.
/// </summary>
public interface IOfficeModelOverrideService
{
    /// <summary>Null when the office has no enabled assignment of its own.</summary>
    Task<OfficeModelOverride?> ResolveOfficeAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Null when the system default is unset, or points at a model that is no longer
    /// enabled/known — a misconfiguration an admin needs to fix from the console.</summary>
    Task<OfficeModelOverride?> ResolveSystemDefaultAsync(CancellationToken ct = default);
}
