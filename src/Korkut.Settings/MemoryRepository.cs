using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Settings;

/// <summary>EF/Postgres adapter for <see cref="IMemoryRepository"/> over the ambient DbContext.</summary>
public sealed class MemoryRepository(DbContext db) : Repository<MemoryItem>(db), IMemoryRepository
{
    public override async Task<IReadOnlyList<MemoryItem>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
}
