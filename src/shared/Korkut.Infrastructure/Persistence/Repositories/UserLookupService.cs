using Korkut.Persistence;
using Korkut.Core;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// Operational-DB adapter for <see cref="IUserLookupService"/>. The author's display name lives
/// in the operational Users table, which the Agent DB never joins; the Agent host calls this
/// to resolve ids when building responses. Each lookup opens its OWN operational tenant scope
/// (the surrounding request is pinned on the Agent DB, a different connection), so RLS still
/// scopes the read to the caller's office. The display name is the user's e-mail (parity with
/// the pre-split behaviour).
/// </summary>
public sealed class UserLookupService(KorkutDbContext db) : IUserLookupService
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
        var map = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        await scope.CommitAsync(ct);
        return map;
    }

    public async Task<string?> EmailAsync(Guid officeId, Guid userId, CancellationToken ct = default)
    {
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var email = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        await scope.CommitAsync(ct);
        return email;
    }
}
