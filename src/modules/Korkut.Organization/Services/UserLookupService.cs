using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Services;

/// <summary>
/// Operational-DB adapter for <see cref="IUserLookupService"/>, owned by the module that owns the
/// Users table. The Agent host stores only the raw author id in its own DB (which never joins
/// operational data); it calls this to resolve ids when building a response. Each lookup opens its
/// OWN operational tenant scope — the surrounding request may be pinned on a different connection
/// (the Agent DB) — so RLS still scopes the read to the caller's office. The display name is the
/// user's full name, falling back to the e-mail when no name is recorded.
/// </summary>
internal sealed class UserLookupService(DbContext db) : IUserLookupService
{
    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
        Guid officeId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = userIds.Distinct().ToList();
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var map = await db.Set<User>().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FullName ?? u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        await scope.CommitAsync(ct);
        return map;
    }

    public async Task<string?> EmailAsync(Guid officeId, Guid userId, CancellationToken ct = default)
    {
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var email = await db.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        await scope.CommitAsync(ct);
        return email;
    }
}
