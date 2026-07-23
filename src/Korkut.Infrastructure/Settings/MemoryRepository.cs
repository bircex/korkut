using Korkut.Application.Settings;
using Korkut.Domain.Settings;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Settings;

/// <summary>EF/Postgres adapter for <see cref="IMemoryRepository"/>.</summary>
public sealed class MemoryRepository(KorkutDbContext db) : Repository<MemoryItem>(db), IMemoryRepository
{
    public override async Task<IReadOnlyList<MemoryItem>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
}
