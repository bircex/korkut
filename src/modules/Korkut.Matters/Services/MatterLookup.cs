using Korkut.Matters.Repositories;
using Korkut.Matters.Entities;
using Korkut.Contract.Matters;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Matters.Services;

/// <summary>
/// Implements the cross-module <see cref="IMatterLookup"/> over the matter repository / the
/// module's own table, reading in the caller's already-open tenant scope. Lets other modules
/// validate or name a matter without ever seeing the Matter type.
/// </summary>
internal sealed class MatterLookup(IMatterRepository repository, DbContext db) : IMatterLookup
{
    public async Task<bool> ExistsAsync(Guid officeId, Guid matterId, CancellationToken ct = default)
        => await repository.FindAsync(officeId, matterId, ct) is not null;

    public async Task<IReadOnlyDictionary<Guid, string>> TitlesAsync(
        Guid officeId, IReadOnlyCollection<Guid> matterIds, CancellationToken ct = default)
    {
        if (matterIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = matterIds.Distinct().ToList();
        return await db.Set<Matter>().AsNoTracking()
            .Where(m => m.OfficeId == officeId && ids.Contains(m.Id))
            .Select(m => new { m.Id, m.Title })
            .ToDictionaryAsync(m => m.Id, m => m.Title, ct);
    }
}
