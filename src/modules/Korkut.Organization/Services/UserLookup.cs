using Korkut.Organization.Entities;
using Korkut.Contract.Organization;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Services;

/// <summary>
/// Implements the cross-module <see cref="IUserLookup"/> over the module's own table, reading in the
/// caller's already-open tenant scope. Lets other modules attribute a row to a user by e-mail
/// without the User type.
/// </summary>
internal sealed class UserLookup(DbContext db) : IUserLookup
{
    public async Task<IReadOnlyDictionary<Guid, string>> EmailsAsync(
        Guid officeId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = userIds.Distinct().ToList();
        return await db.Set<User>().AsNoTracking()
            .Where(u => u.OfficeId == officeId && ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);
    }
}
