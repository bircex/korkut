using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Matters;

/// <summary>EF/Postgres adapter for <see cref="IMatterRepository"/>: active matters, newest first.</summary>
public sealed class MatterRepository(DbContext db) : SoftDeleteRepository<Matter>(db), IMatterRepository
{
    public override async Task<IReadOnlyList<Matter>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
}
