using Korkut.Core;
using Korkut.Organization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContract = Korkut.Contract.Agent.Model;

namespace Korkut.Organization.Services;

/// <summary>
/// Guarantees the system default is never a silent gap. The routing chain (see
/// <see cref="ModelContract.IOfficeModelOverrideService"/>) has no tier below the system default —
/// on purpose, so nothing in the codebase ever hardcodes a preferred vendor — which means a fresh
/// install needs SOMETHING there before the first office can chat.
///
/// This runs once, at startup, on every host that wires the Organization module (Api AND Agent —
/// both already hold the provider keys). It is idempotent and order-independent: if the system
/// default is already set (an admin picked one, or another replica's copy of this check won),
/// it does nothing. Only when it is genuinely unset does it pick the first ENABLED catalog entry,
/// by sort order, whose provider has a configured key — and writes it as an ORDINARY
/// <see cref="PlatformSetting"/> row. From that moment it is indistinguishable from a value an
/// admin set by hand: fully dashboard-editable, invalidated through the same cache, no special
/// casing anywhere in the routing path. If nothing is configured yet (no catalog, or no provider
/// key anywhere), it leaves the gap for an admin to fill — it does not invent a choice from thin air.
/// </summary>
internal sealed partial class PlatformDefaultBootstrapper(
    IServiceScopeFactory scopeFactory, ILogger<PlatformDefaultBootstrapper> logger) : IHostedService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Sistem varsayılan modeli bootstrap kontrolü atlandı (DB henüz erişilebilir değil olabilir).")]
    private static partial void LogBootstrapSkipped(ILogger logger, Exception ex);

    public async Task StartAsync(CancellationToken ct)
    {
        // Best-effort ONLY: this is a startup nicety, never a precondition for the host to come up.
        // The DB may not be reachable yet (migrator still running, a rolling restart, a test host
        // with no real database at all) — none of that may ever block the host from starting, so
        // every failure here is logged and swallowed, not rethrown. Missing this bootstrap once just
        // means the system default stays unset until the next host start or an admin sets it by hand.
        try
        {
            await RunAsync(ct);
        }
        catch (Exception ex)
        {
            LogBootstrapSkipped(logger, ex);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var env = scope.ServiceProvider.GetRequiredService<IEnvironment>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var setting = await db.Set<PlatformSetting>()
            .FirstOrDefaultAsync(s => s.Key == PlatformSetting.DefaultModelKey, ct);
        if (!string.IsNullOrWhiteSpace(setting?.Value))
        {
            return;
        }

        var catalog = await db.Set<ModelCatalogEntry>().AsNoTracking()
            .Where(m => m.Enabled)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
            .ToListAsync(ct);
        var chosen = catalog.FirstOrDefault(m => ModelContract.ChatProviders.Find(m.Provider) is { } descriptor && IsConfigured(env, descriptor));
        if (chosen is null)
        {
            return;
        }

        var now = clock.UtcNow;
        if (setting is null)
        {
            db.Set<PlatformSetting>().Add(new PlatformSetting(PlatformSetting.DefaultModelKey, chosen.Key, now));
        }
        else
        {
            setting.Set(chosen.Key, now);
        }

        // A benign race between replicas both finding it unset: last write wins on the same
        // deterministic pick (sort order is stable), so a concurrency conflict here is harmless —
        // swallow it too rather than letting it propagate to the outer catch as a scary warning.
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>Key PRESENCE only — the value is never read here, only whether the operator set it.</summary>
    private static bool IsConfigured(IEnvironment env, ModelContract.ChatProviderDescriptor provider)
        => provider.ApiKeyEnvVar is null || !string.IsNullOrWhiteSpace(env.Get(provider.ApiKeyEnvVar));
}
