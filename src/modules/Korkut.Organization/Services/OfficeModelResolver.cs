using ModelContract = Korkut.Contract.Agent.Model;

namespace Korkut.Organization.Services;

/// <summary>
/// The chat router's per-request answer to "which model does this office use?" — split into the
/// office pin and the system default because the caller (RoutingChatModelService) interleaves a
/// third, specialty-driven decision (local backend for hearing) BETWEEN them; see
/// <see cref="ModelContract.IOfficeModelOverrideService"/> for the full precedence chain this is
/// one piece of.
///
/// A catalog entry that is disabled, missing, or points at a provider this build cannot route to is
/// treated as unset, so retiring a model can never strand an office (or the whole platform) on a
/// dead selection — the office pin degrades to the system default; the system default degrades to
/// null, which the caller turns into a loud, actionable error rather than a silent vendor guess.
///
/// Reads come from <see cref="ModelPlaneReader"/>'s cached snapshot, which is why flipping the
/// default in the dashboard takes effect without restarting anything.
/// </summary>
public sealed class OfficeModelResolver(ModelPlaneReader plane) : ModelContract.IOfficeModelOverrideService
{
    public async Task<ModelContract.OfficeModelOverride?> ResolveOfficeAsync(Guid officeId, CancellationToken ct = default)
        => ResolveOffice(await plane.LoadAsync(ct), officeId);

    public async Task<ModelContract.OfficeModelOverride?> ResolveSystemDefaultAsync(CancellationToken ct = default)
        => ResolveSystemDefault(await plane.LoadAsync(ct));

    /// <summary>Pure functions of a loaded snapshot — separated from the loading so the precedence
    /// rules can be tested exhaustively without a database.</summary>
    public static ModelContract.OfficeModelOverride? ResolveOffice(ModelPlaneSnapshot snapshot, Guid officeId)
        => snapshot.OfficeAssignments.TryGetValue(officeId, out var officeKey) && Usable(snapshot, officeKey) is { } pinned
            ? Selection(pinned, ModelContract.ModelSelectionSource.Office)
            : null;

    public static ModelContract.OfficeModelOverride? ResolveSystemDefault(ModelPlaneSnapshot snapshot)
        => snapshot.DefaultModelKey is { } defaultKey && Usable(snapshot, defaultKey) is { } row
            ? Selection(row, ModelContract.ModelSelectionSource.SystemDefault)
            : null;

    private static CatalogRow? Usable(ModelPlaneSnapshot snapshot, string key)
        => snapshot.Catalog.TryGetValue(key, out var row)
            && row.Enabled
            && !string.IsNullOrWhiteSpace(row.Provider)
            && !string.IsNullOrWhiteSpace(row.ModelId)
            && ModelContract.ChatProviders.Find(row.Provider) is not null
                ? row
                : null;

    private static ModelContract.OfficeModelOverride Selection(CatalogRow row, string source)
        => new(row.Provider, row.ModelId, row.MaxTokens, source, row.Key);
}
