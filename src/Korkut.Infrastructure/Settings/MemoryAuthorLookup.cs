using Korkut.Application.Settings;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Settings;

/// <summary>
/// EF/Postgres adapter for <see cref="IMemoryAuthorLookup"/>. The read joins nothing and
/// opens nothing: it runs in the tenant scope the use-case already holds, and RLS keeps it
/// to the caller's office. The display value is the user's e-mail.
/// </summary>
public sealed class MemoryAuthorLookup(KorkutDbContext db) : IMemoryAuthorLookup
{
    public async Task<IReadOnlyDictionary<Guid, string>> EmailsAsync(
        Guid officeId, IReadOnlyCollection<Guid> authorIds, CancellationToken ct = default)
    {
        if (authorIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = authorIds.Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(u => u.OfficeId == officeId && ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);
    }
}
