using Korkut.Application.Matters;
using Korkut.Domain.Matters;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Matters;

/// <summary>EF/Postgres adapter for <see cref="IMatterRepository"/>: active matters, newest first.</summary>
public sealed class MatterRepository(KorkutDbContext db) : SoftDeleteRepository<Matter>(db), IMatterRepository
{
    public override async Task<IReadOnlyList<Matter>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
}
