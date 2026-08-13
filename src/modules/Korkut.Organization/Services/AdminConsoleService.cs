using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.Organization.Repositories;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;
using ModelContract = Korkut.Contract.Agent.Model;

namespace Korkut.Organization.Services;

/// <summary>
/// The admin console's data surface. Offices are cross-tenant (no scope); per-office reads
/// (members, chat counts) open their OWN short tenant scope per office — the pattern
/// <see cref="UserLookupService"/> established — so RLS still applies to every tenant table.
/// Chat counts are raw-SQL (the Organization module deliberately doesn't reference the Chat
/// module's entities).
///
/// The model catalog lives in the DB (<see cref="ModelCatalogService"/>) and per-office assignment
/// here is LIVE routing, not bookkeeping: an assignment wins over the system default, which is the
/// floor of the whole chain (<see cref="OfficeModelResolver"/> owns it — there is no hardcoded
/// vendor beneath it). Because an office that is NOT pinned still resolves to something (a healthy
/// system always has a system default), every office row also reports its EFFECTIVE model, so the
/// console shows the answer rather than making an admin infer it.
///
/// Token/cost totals come from the Agent DB's ledger through <see cref="IUsageReportService"/>.
/// </summary>
public sealed class AdminConsoleService(
    DbContext db, IOfficeRepository offices, IClock clock, ModelPlaneReader plane,
    ModelPlaneCache cache, IModelCatalogService catalog, IUsageReportService usage)
    : IAdminConsoleService
{
    public async Task<Result<AdminStatsResponse>> StatsAsync(CancellationToken ct = default)
    {
        var all = await offices.ListAsync(ct);
        var users = 0;
        foreach (var office in all)
        {
            await using var scope = await db.BeginTenantScopeAsync(office.Id, ct);
            users += await db.Set<User>().CountAsync(u => u.OfficeId == office.Id, ct);
            await scope.CommitAsync(ct);
        }

        var disabled = all.Count(o => o.Disabled);
        return Result<AdminStatsResponse>.Ok(new AdminStatsResponse(all.Count, all.Count - disabled, disabled, users));
    }

    public async Task<Result<IReadOnlyList<AdminOfficeResponse>>> OfficesAsync(CancellationToken ct = default)
    {
        var all = await offices.ListAsync(ct);
        var assignments = await db.Set<AdminOfficeModel>().AsNoTracking().ToListAsync(ct);
        var snapshot = await plane.LoadAsync(ct);
        var rows = new List<AdminOfficeResponse>(all.Count);
        foreach (var office in all)
        {
            await using var scope = await db.BeginTenantScopeAsync(office.Id, ct);
            var members = await db.Set<User>().CountAsync(u => u.OfficeId == office.Id, ct);
            var owner = await db.Set<User>().AsNoTracking()
                .Where(u => u.OfficeId == office.Id && u.Role == "owner")
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
            await scope.CommitAsync(ct);
            rows.Add(new AdminOfficeResponse(
                office.Id, office.Name, office.Disabled, owner, members, office.CreatedAt,
                ModelState(snapshot, assignments.FirstOrDefault(a => a.OfficeId == office.Id))));
        }

        return Result<IReadOnlyList<AdminOfficeResponse>>.Ok(rows);
    }

    public async Task<Result<IReadOnlyList<AdminUserResponse>>> UsersAsync(CancellationToken ct = default)
    {
        var all = await offices.ListAsync(ct);
        var rows = new List<AdminUserResponse>();
        foreach (var office in all)
        {
            await using var scope = await db.BeginTenantScopeAsync(office.Id, ct);
            var members = await db.Set<User>().AsNoTracking()
                .Where(u => u.OfficeId == office.Id)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new { u.Id, u.Email, u.FullName, u.Role, u.CreatedAt })
                .ToListAsync(ct);
            await scope.CommitAsync(ct);
            rows.AddRange(members.Select(m => new AdminUserResponse(
                m.Id, m.Email, m.FullName, m.Role, office.Id, office.Name, office.Disabled, m.CreatedAt)));
        }

        return Result<IReadOnlyList<AdminUserResponse>>.Ok(rows);
    }

    public async Task<Result<AdminOfficeDetailResponse>> OfficeDetailAsync(Guid officeId, CancellationToken ct = default)
    {
        var office = await offices.FindAsync(officeId, ct);
        if (office is null)
        {
            return Result<AdminOfficeDetailResponse>.NotFound("Ofis bulunamadı.");
        }

        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var members = await db.Set<User>().AsNoTracking()
            .Where(u => u.OfficeId == officeId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new { u.Id, u.Email, u.FullName, u.Role })
            .ToListAsync(ct);
        var chats = await CountAsync(
            $"""SELECT count(*)::int AS "Value" FROM korkut.conversations WHERE office_id = {officeId}""", ct);
        var messages = await CountAsync(
            $"""SELECT count(*)::int AS "Value" FROM korkut.chat_messages WHERE office_id = {officeId}""", ct);
        var perMemberCounts = new Dictionary<Guid, (int Chats, int Messages)>(members.Count);
        foreach (var m in members)
        {
            var mChats = await CountAsync(
                $"""SELECT count(*)::int AS "Value" FROM korkut.conversations WHERE office_id = {officeId} AND created_by = {m.Id}""", ct);
            var mMessages = await CountAsync(
                $"""SELECT count(*)::int AS "Value" FROM korkut.chat_messages cm WHERE cm.office_id = {officeId} AND cm.conversation_id IN (SELECT c.id FROM korkut.conversations c WHERE c.office_id = {officeId} AND c.created_by = {m.Id})""", ct);
            perMemberCounts[m.Id] = (mChats, mMessages);
        }

        await scope.CommitAsync(ct);

        // Lifetime token/cost from the ledger (own tenant scope; the one above is already committed).
        var report = await usage.OfficeUsageAsync(officeId, Epoch, clock.UtcNow.AddDays(1), ct);
        var totals = report.Value?.Totals ?? AdminUsageTotals.Empty;
        var perMemberUsage = (report.Value?.ByMember ?? [])
            .ToDictionary(r => r.Key, r => r.Totals, StringComparer.Ordinal);

        var perMember = members.Select(m => new AdminOfficeMemberResponse(
                m.Id, m.Email, m.FullName, m.Role,
                perMemberCounts[m.Id].Chats, perMemberCounts[m.Id].Messages,
                perMemberUsage.GetValueOrDefault(m.Id.ToString(), AdminUsageTotals.Empty)))
            .ToList();

        var snapshot = await plane.LoadAsync(ct);
        var assignment = await db.Set<AdminOfficeModel>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfficeId == officeId, ct);

        return Result<AdminOfficeDetailResponse>.Ok(new AdminOfficeDetailResponse(
            office.Id, office.Name, office.Disabled, office.CreatedAt,
            ModelState(snapshot, assignment),
            new AdminOfficeTotals(chats, messages, totals.TotalTokens, totals.Cost),
            perMember));
    }

    /// <summary>The catalog as the console lists it — delegated to the module that owns it.</summary>
    public Task<Result<IReadOnlyList<AdminModelResponse>>> ModelsAsync(CancellationToken ct = default)
        => catalog.ListAsync(ct);

    public async Task<Result<AdminOfficeDetailResponse>> AssignModelAsync(
        Guid officeId, string? modelKey, bool enabled, CancellationToken ct = default)
    {
        var key = string.IsNullOrWhiteSpace(modelKey) ? null : modelKey.Trim();
        if (key is not null)
        {
            var entry = await db.Set<ModelCatalogEntry>().AsNoTracking().FirstOrDefaultAsync(m => m.Key == key, ct);
            if (entry is null)
            {
                return Result<AdminOfficeDetailResponse>.BadRequest("Bilinmeyen model anahtarı.");
            }

            // Only block on a disabled model when the assignment is actually being turned ON;
            // an admin must still be able to park a disabled key with the toggle off.
            if (enabled && !entry.Enabled)
            {
                return Result<AdminOfficeDetailResponse>.BadRequest("Devre dışı bir model büroya atanamaz.");
            }
        }

        var assignment = await db.Set<AdminOfficeModel>().FirstOrDefaultAsync(x => x.OfficeId == officeId, ct);
        if (assignment is null)
        {
            db.Set<AdminOfficeModel>().Add(new AdminOfficeModel(officeId, key, enabled, clock.UtcNow));
        }
        else
        {
            assignment.Assign(key, enabled, clock.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        // The chat router reads assignments through the cached snapshot; drop it so this replica
        // routes by the new assignment on the very next turn.
        cache.Invalidate();
        return await OfficeDetailAsync(officeId, ct);
    }

    /// <summary>
    /// What this office is pinned to AND what it actually resolves to — mirrors the precedence chain
    /// in <see cref="OfficeModelResolver"/>: office assignment, then the system default (the floor —
    /// nothing beneath it). Reaching <see cref="ModelContract.ModelSelectionSource.Unconfigured"/>
    /// here means the system default is unset or broken, a real misconfiguration to flag, not a
    /// supported steady state. (This console-side mirror is specialty-agnostic — the local/hearing
    /// preference is a chat-time-only decision the office list has no reason to reflect.)
    /// </summary>
    private static AdminOfficeModelState ModelState(ModelPlaneSnapshot snapshot, AdminOfficeModel? assignment)
    {
        var pinnedLabel = assignment?.ModelKey is { } k ? snapshot.Catalog.GetValueOrDefault(k)?.Label : null;

        if (assignment is { Enabled: true, ModelKey: { } key } && snapshot.Catalog.TryGetValue(key, out var pinned)
            && pinned.Enabled)
        {
            return new AdminOfficeModelState(
                true, key, pinned.Label, key, pinned.Label, ModelContract.ModelSelectionSource.Office);
        }

        if (snapshot.DefaultModelKey is { } defaultKey
            && snapshot.Catalog.TryGetValue(defaultKey, out var fallback) && fallback.Enabled)
        {
            return new AdminOfficeModelState(
                assignment?.Enabled ?? false, assignment?.ModelKey, pinnedLabel,
                defaultKey, fallback.Label, ModelContract.ModelSelectionSource.SystemDefault);
        }

        return new AdminOfficeModelState(
            assignment?.Enabled ?? false, assignment?.ModelKey, pinnedLabel,
            null, null, ModelContract.ModelSelectionSource.Unconfigured);
    }

    /// <summary>Parameterized raw-SQL count; the caller already holds the tenant scope.</summary>
    private Task<int> CountAsync(FormattableString sql, CancellationToken ct)
        => db.Database.SqlQuery<int>(sql).SingleAsync(ct);

    /// <summary>Lower bound for "all time" ledger reads — the ledger cannot predate the platform.</summary>
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;
}
