using System.Text.Json;
using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.Organization.Repositories;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Services;

/// <summary>
/// The admin console's data surface. Offices are cross-tenant (no scope); per-office reads
/// (members, chat counts) open their OWN short tenant scope per office — the pattern
/// <see cref="UserLookupService"/> established — so RLS still applies to every tenant table.
/// Chat counts are raw-SQL (the Organization module deliberately doesn't reference the Chat
/// module's entities). The model catalog is the operator's read-only JSON file
/// (KORKUT_MODELS_FILE); assignment is bookkeeping only — no API keys, and the runtime router
/// does not consume it yet. Token totals come from the Agent DB, which this host cannot see,
/// so they are reported as 0 until the ledger is surfaced.
/// </summary>
public sealed class AdminConsoleService(
    DbContext db, IOfficeRepository offices, IEnvironment env, IClock clock) : IAdminConsoleService
{
    private sealed record CatalogModel(
        string? Key, string? Label, string? Model, string? BaseUrl, bool IsLocal);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

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
            rows.Add(new AdminOfficeResponse(office.Id, office.Name, office.Disabled, owner, members, office.CreatedAt));
        }

        return Result<IReadOnlyList<AdminOfficeResponse>>.Ok(rows);
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
        var perMember = new List<AdminOfficeMemberResponse>(members.Count);
        foreach (var m in members)
        {
            var mChats = await CountAsync(
                $"""SELECT count(*)::int AS "Value" FROM korkut.conversations WHERE office_id = {officeId} AND created_by = {m.Id}""", ct);
            var mMessages = await CountAsync(
                $"""SELECT count(*)::int AS "Value" FROM korkut.chat_messages cm WHERE cm.office_id = {officeId} AND cm.conversation_id IN (SELECT c.id FROM korkut.conversations c WHERE c.office_id = {officeId} AND c.created_by = {m.Id})""", ct);
            perMember.Add(new AdminOfficeMemberResponse(m.Id, m.Email, m.FullName, m.Role, mChats, mMessages));
        }

        await scope.CommitAsync(ct);

        var assignment = await db.Set<AdminOfficeModel>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfficeId == officeId, ct);
        var catalog = LoadCatalog();
        var label = assignment?.ModelKey is { } key
            ? catalog.FirstOrDefault(c => c.Key == key)?.Label
            : null;

        return Result<AdminOfficeDetailResponse>.Ok(new AdminOfficeDetailResponse(
            office.Id, office.Name, office.Disabled, office.CreatedAt,
            new AdminOfficeModelState(assignment?.Enabled ?? false, assignment?.ModelKey, label),
            new AdminOfficeTotals(chats, messages, Tokens: 0),
            perMember));
    }

    public Task<Result<IReadOnlyList<AdminModelResponse>>> ModelsAsync(CancellationToken ct = default)
        => Task.FromResult(Result<IReadOnlyList<AdminModelResponse>>.Ok(LoadCatalog()));

    public async Task<Result<AdminOfficeDetailResponse>> AssignModelAsync(
        Guid officeId, string? modelKey, bool enabled, CancellationToken ct = default)
    {
        if (modelKey is not null && LoadCatalog().All(c => c.Key != modelKey))
        {
            return Result<AdminOfficeDetailResponse>.BadRequest("Bilinmeyen model anahtarı.");
        }

        var assignment = await db.Set<AdminOfficeModel>().FirstOrDefaultAsync(x => x.OfficeId == officeId, ct);
        if (assignment is null)
        {
            db.Set<AdminOfficeModel>().Add(new AdminOfficeModel(officeId, modelKey, enabled, clock.UtcNow));
        }
        else
        {
            assignment.Assign(modelKey, enabled, clock.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        return await OfficeDetailAsync(officeId, ct);
    }

    private List<AdminModelResponse> LoadCatalog()
    {
        var path = env.Get("KORKUT_MODELS_FILE");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<CatalogModel>>(File.ReadAllText(path), Json) ?? [];
            return parsed
                .Where(m => !string.IsNullOrWhiteSpace(m.Key))
                .Select(m => new AdminModelResponse(
                    m.Key!, m.Label ?? m.Key!, m.Model ?? string.Empty, m.BaseUrl ?? string.Empty, m.IsLocal))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Parameterized raw-SQL count; the caller already holds the tenant scope.</summary>
    private Task<int> CountAsync(FormattableString sql, CancellationToken ct)
        => db.Database.SqlQuery<int>(sql).SingleAsync(ct);
}
