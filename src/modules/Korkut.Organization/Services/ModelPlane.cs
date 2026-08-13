using Korkut.Core;
using Korkut.Organization.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Services;

/// <summary>One catalog entry as the router needs it — no display or bookkeeping fields.</summary>
public sealed record CatalogRow(string Key, string Label, string Provider, string ModelId, int? MaxTokens, bool Enabled);

/// <summary>
/// A consistent read of the whole model plane: the catalog, the platform default key, and every
/// office's enabled assignment. Both tables are admin-global and tiny (one row per office at most),
/// so loading them together is cheaper than the per-request lookups it replaces.
/// </summary>
public sealed record ModelPlaneSnapshot(
    DateTimeOffset LoadedAt,
    IReadOnlyDictionary<string, CatalogRow> Catalog,
    string? DefaultModelKey,
    IReadOnlyDictionary<Guid, string> OfficeAssignments);

/// <summary>
/// Process-wide cache of <see cref="ModelPlaneSnapshot"/>, so the chat router does not hit the DB on
/// every turn. Deliberately TTL-based rather than event-invalidated: the Agent host is a separate
/// process from the Api host that writes these tables, so there is nothing to notify. The TTL is the
/// worst-case lag between an admin flipping the default in the dashboard and every replica honouring
/// it — short enough to feel immediate, long enough that a busy office is not re-reading config per
/// message. The writing process additionally calls <see cref="Invalidate"/> so ITS next read is
/// already fresh (the console never shows a stale echo of the change just made).
/// </summary>
public sealed class ModelPlaneCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);

    private volatile ModelPlaneSnapshot? _snapshot;

    public ModelPlaneSnapshot? Fresh(DateTimeOffset now)
        => _snapshot is { } s && now - s.LoadedAt < Ttl ? s : null;

    public void Put(ModelPlaneSnapshot snapshot) => _snapshot = snapshot;

    public void Invalidate() => _snapshot = null;
}

/// <summary>
/// Loads (and caches) the model plane. Scoped, because it needs the request's <see cref="DbContext"/>;
/// the cache it fills is the singleton above. Both tables are cross-tenant admin config with no RLS,
/// so these reads need no tenant scope.
/// </summary>
public sealed class ModelPlaneReader(DbContext db, IClock clock, ModelPlaneCache cache)
{
    public async Task<ModelPlaneSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        if (cache.Fresh(now) is { } cached)
        {
            return cached;
        }

        var catalog = await db.Set<ModelCatalogEntry>().AsNoTracking()
            .Select(m => new CatalogRow(m.Key, m.Label, m.Provider, m.ModelId, m.MaxTokens, m.Enabled))
            .ToListAsync(ct);
        var defaultKey = await db.Set<PlatformSetting>().AsNoTracking()
            .Where(s => s.Key == PlatformSetting.DefaultModelKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        var assignments = await db.Set<AdminOfficeModel>().AsNoTracking()
            .Where(a => a.Enabled && a.ModelKey != null)
            .Select(a => new { a.OfficeId, a.ModelKey })
            .ToListAsync(ct);

        var snapshot = new ModelPlaneSnapshot(
            now,
            catalog.ToDictionary(c => c.Key, StringComparer.Ordinal),
            string.IsNullOrWhiteSpace(defaultKey) ? null : defaultKey,
            // An office row is unique per office (ux_admin_office_models_office), but tolerate a
            // duplicate rather than throwing on the chat path.
            assignments
                .GroupBy(a => a.OfficeId)
                .ToDictionary(g => g.Key, g => g.First().ModelKey!));

        cache.Put(snapshot);
        return snapshot;
    }
}
