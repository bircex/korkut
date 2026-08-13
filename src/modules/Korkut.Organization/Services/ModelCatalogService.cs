using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.Organization.Repositories;
using Microsoft.EntityFrameworkCore;
using ModelContract = Korkut.Contract.Agent.Model;

namespace Korkut.Organization.Services;

/// <summary>
/// The admin console's model plane: the DB-backed catalog (which replaced the operator's read-only
/// KORKUT_MODELS_FILE) and the one-switch platform default. Every write invalidates the shared
/// <see cref="ModelPlaneCache"/>, so a change is live for the next chat turn on this replica and
/// within the cache TTL everywhere else — no redeploy, no restart.
///
/// API keys are NOT part of this surface, by design: a catalog entry only NAMES a provider, and that
/// provider reads its key from the environment at call time (see
/// <see cref="ModelContract.ChatProviders"/>). That is why <see cref="ProvidersAsync"/> reports mere
/// key PRESENCE — enough for the console to grey out a provider nobody can call, without a secret
/// ever crossing this boundary.
/// </summary>
public sealed class ModelCatalogService(
    DbContext db, IOfficeRepository offices, ModelPlaneReader plane, ModelPlaneCache cache,
    IEnvironment env, IClock clock) : IModelCatalogService
{
    public async Task<Result<IReadOnlyList<AdminModelResponse>>> ListAsync(CancellationToken ct = default)
    {
        var rows = await ListRowsAsync(ct);
        return Result<IReadOnlyList<AdminModelResponse>>.Ok(rows);
    }

    public async Task<Result<AdminModelResponse>> UpsertAsync(
        AdminModelUpsertRequest request, CancellationToken ct = default)
    {
        var key = request.Key?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            return Result<AdminModelResponse>.BadRequest("Model anahtarı zorunludur.");
        }

        var provider = request.Provider?.Trim() ?? string.Empty;
        if (ModelContract.ChatProviders.Find(provider) is null)
        {
            return Result<AdminModelResponse>.BadRequest($"Bilinmeyen sağlayıcı: {provider}");
        }

        var modelId = request.ModelId?.Trim() ?? string.Empty;
        if (modelId.Length == 0)
        {
            return Result<AdminModelResponse>.BadRequest("Model kimliği zorunludur.");
        }

        if (request.MaxTokens is <= 0)
        {
            return Result<AdminModelResponse>.BadRequest("Azami token sayısı pozitif olmalıdır.");
        }

        // Disabling the system default would strand every office without its own pin the moment
        // this saves — same invariant SetDefaultAsync/DeleteAsync protect, just reached from the
        // catalog editor instead.
        if (!request.Enabled)
        {
            var snapshot = await plane.LoadAsync(ct);
            if (snapshot.DefaultModelKey == key)
            {
                return Result<AdminModelResponse>.Conflict(
                    "Bu model sistem varsayılanı olduğu için devre dışı bırakılamaz. Önce varsayılanı değiştirin.");
            }
        }

        var label = string.IsNullOrWhiteSpace(request.Label) ? key : request.Label.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();
        var now = clock.UtcNow;

        var existing = await db.Set<ModelCatalogEntry>().FirstOrDefaultAsync(m => m.Key == key, ct);
        if (existing is null)
        {
            db.Set<ModelCatalogEntry>().Add(new ModelCatalogEntry(
                key, label, provider, modelId, baseUrl, request.MaxTokens, request.IsLocal,
                request.Enabled, request.SortOrder, now));
        }
        else
        {
            existing.Update(
                label, provider, modelId, baseUrl, request.MaxTokens, request.IsLocal,
                request.Enabled, request.SortOrder, now);
        }

        await db.SaveChangesAsync(ct);
        cache.Invalidate();

        var saved = (await ListRowsAsync(ct)).FirstOrDefault(m => m.Key == key);
        return saved is null
            ? Result<AdminModelResponse>.NotFound("Model kaydedildi ancak okunamadı.")
            : Result<AdminModelResponse>.Ok(saved);
    }

    public async Task<Result> DeleteAsync(string key, CancellationToken ct = default)
    {
        var entry = await db.Set<ModelCatalogEntry>().FirstOrDefaultAsync(m => m.Key == key, ct);
        if (entry is null)
        {
            return Result.NotFound("Model bulunamadı.");
        }

        // Refuse while something still points here — deleting would silently re-route live traffic.
        var snapshot = await plane.LoadAsync(ct);
        if (snapshot.DefaultModelKey == key)
        {
            return Result.Conflict("Bu model platform varsayılanı olduğu için silinemez. Önce varsayılanı değiştirin.");
        }

        var assigned = await db.Set<AdminOfficeModel>().CountAsync(a => a.ModelKey == key, ct);
        if (assigned > 0)
        {
            return Result.Conflict($"Bu model {assigned} büroya atanmış durumda. Önce atamaları kaldırın.");
        }

        db.Set<ModelCatalogEntry>().Remove(entry);
        await db.SaveChangesAsync(ct);
        cache.Invalidate();
        return Result.Ok();
    }

    public Task<Result<IReadOnlyList<AdminProviderResponse>>> ProvidersAsync(CancellationToken ct = default)
    {
        var rows = ModelContract.ChatProviders.All
            .Select(p => new AdminProviderResponse(p.Name, p.Label, IsConfigured(p), p.IsLocal))
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<AdminProviderResponse>>.Ok(rows));
    }

    public async Task<Result<AdminPlatformDefaultResponse>> GetDefaultAsync(CancellationToken ct = default)
        => Result<AdminPlatformDefaultResponse>.Ok(await ReadDefaultAsync(ct));

    public async Task<Result<AdminPlatformDefaultResponse>> SetDefaultAsync(
        string modelKey, CancellationToken ct = default)
    {
        var key = modelKey?.Trim();
        // The system default is the floor of the whole routing chain (see
        // IOfficeModelOverrideService) — there is no lower tier, no hardcoded vendor to fall back
        // to. Unsetting it would strand every office that has no pin of its own, so an admin
        // REPLACES it, never clears it: this is the one write this service refuses outright.
        if (string.IsNullOrEmpty(key))
        {
            return Result<AdminPlatformDefaultResponse>.BadRequest(
                "Sistem varsayılan modeli boş bırakılamaz. Bunun yerine başka bir model seçin.");
        }

        var entry = await db.Set<ModelCatalogEntry>().AsNoTracking().FirstOrDefaultAsync(m => m.Key == key, ct);
        if (entry is null)
        {
            return Result<AdminPlatformDefaultResponse>.BadRequest("Bilinmeyen model anahtarı.");
        }

        if (!entry.Enabled)
        {
            return Result<AdminPlatformDefaultResponse>.BadRequest("Devre dışı bir model varsayılan yapılamaz.");
        }

        // A default whose provider has no key would fail every unassigned office's next turn —
        // catch it here, where the admin can still see why, rather than at chat time.
        var descriptor = ModelContract.ChatProviders.Find(entry.Provider);
        if (descriptor is null)
        {
            return Result<AdminPlatformDefaultResponse>.BadRequest($"Bilinmeyen sağlayıcı: {entry.Provider}");
        }

        if (!IsConfigured(descriptor))
        {
            return Result<AdminPlatformDefaultResponse>.BadRequest(
                $"{descriptor.Label} için API anahtarı yapılandırılmamış ({descriptor.ApiKeyEnvVar}).");
        }

        var now = clock.UtcNow;
        var setting = await db.Set<PlatformSetting>()
            .FirstOrDefaultAsync(s => s.Key == PlatformSetting.DefaultModelKey, ct);
        if (setting is null)
        {
            db.Set<PlatformSetting>().Add(new PlatformSetting(PlatformSetting.DefaultModelKey, key, now));
        }
        else
        {
            setting.Set(key, now);
        }

        await db.SaveChangesAsync(ct);
        cache.Invalidate();
        return Result<AdminPlatformDefaultResponse>.Ok(await ReadDefaultAsync(ct));
    }

    private async Task<AdminPlatformDefaultResponse> ReadDefaultAsync(CancellationToken ct)
    {
        var setting = await db.Set<PlatformSetting>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == PlatformSetting.DefaultModelKey, ct);
        var key = string.IsNullOrWhiteSpace(setting?.Value) ? null : setting!.Value;
        if (key is null)
        {
            return new AdminPlatformDefaultResponse(null, null, null, null, setting?.UpdatedAt);
        }

        var entry = await db.Set<ModelCatalogEntry>().AsNoTracking().FirstOrDefaultAsync(m => m.Key == key, ct);
        return new AdminPlatformDefaultResponse(key, entry?.Label, entry?.Provider, entry?.ModelId, setting?.UpdatedAt);
    }

    private async Task<List<AdminModelResponse>> ListRowsAsync(CancellationToken ct)
    {
        var entries = await db.Set<ModelCatalogEntry>().AsNoTracking()
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
            .ToListAsync(ct);
        var assignments = await db.Set<AdminOfficeModel>().AsNoTracking()
            .Where(a => a.ModelKey != null)
            .ToListAsync(ct);
        var names = (await offices.ListAsync(ct)).ToDictionary(o => o.Id, o => o.Name);
        var defaultKey = (await ReadDefaultAsync(ct)).ModelKey;

        return entries.Select(m => new AdminModelResponse(
                m.Key, m.Label, m.ModelId, m.BaseUrl ?? string.Empty, m.IsLocal, m.Provider,
                assignments
                    .Where(a => a.ModelKey == m.Key && names.ContainsKey(a.OfficeId))
                    .Select(a => new AdminModelOfficeRef(a.OfficeId, names[a.OfficeId], a.Enabled))
                    .OrderBy(o => o.Name)
                    .ToList(),
                m.MaxTokens, m.Enabled, m.SortOrder,
                ProviderConfigured: ModelContract.ChatProviders.Find(m.Provider) is { } d && IsConfigured(d),
                IsPlatformDefault: m.Key == defaultKey))
            .ToList();
    }

    /// <summary>Key PRESENCE only — the value is never read here, only whether the operator set it.</summary>
    private bool IsConfigured(ModelContract.ChatProviderDescriptor provider)
        => provider.ApiKeyEnvVar is null || !string.IsNullOrWhiteSpace(env.Get(provider.ApiKeyEnvVar));
}
