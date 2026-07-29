using Korkut.Correspondence.Entities;
using Korkut.Contract.Correspondence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Correspondence.Services;

/// <summary>
/// Implements the cross-module <see cref="ICorrespondenceLookup"/> over the module's own table,
/// reading in the caller's already-open tenant scope. Lets the dashboard tally evrak by direction
/// without the CorrespondenceItem type.
/// </summary>
internal sealed class CorrespondenceLookup(DbContext db) : ICorrespondenceLookup
{
    public async Task<IReadOnlyDictionary<string, int>> DirectionCountsAsync(Guid officeId, CancellationToken ct = default)
        => await db.Set<CorrespondenceItem>().AsNoTracking()
            .Where(e => e.OfficeId == officeId)
            .GroupBy(e => e.Direction)
            .Select(g => new { Direction = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Direction, g => g.Count, ct);
}
