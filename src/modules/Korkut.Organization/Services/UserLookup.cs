using Korkut.Organization.Entities;
using Korkut.Contract.Organization;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Services;

/// <summary>
/// Implements the cross-module <see cref="IUserLookup"/> over the module's own table, reading in the
/// caller's already-open tenant scope. Lets other modules attribute a row to a user by display
/// name (full name, e-mail fallback) without the User type.
/// </summary>
internal sealed class UserLookup(DbContext db) : IUserLookup
{
    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
        Guid officeId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = userIds.Distinct().ToList();
        return await db.Set<User>().AsNoTracking()
            .Where(u => u.OfficeId == officeId && ids.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FullName ?? u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    public Task<string?> RoleAsync(Guid officeId, Guid userId, CancellationToken ct = default)
        => db.Set<User>().AsNoTracking()
            .Where(u => u.OfficeId == officeId && u.Id == userId)
            .Select(u => u.Role)
            .SingleOrDefaultAsync(ct);

    public Task<string?> OwnerEmailAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<User>().AsNoTracking()
            .Where(u => u.OfficeId == officeId && u.Role == Korkut.Core.Roles.OfficeOwner)
            .OrderBy(u => u.CreatedAt)
            .Select(u => (string?)u.Email)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> DirectoryAsync(Guid officeId, CancellationToken ct = default)
        => await db.Set<User>().AsNoTracking()
            .Where(u => u.OfficeId == officeId)
            .Select(u => new { u.Id, Name = u.FullName ?? u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
}
