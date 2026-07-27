using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Deadlines;

/// <summary>EF/Postgres adapter for <see cref="IDeadlineRepository"/> over the ambient DbContext.</summary>
public sealed class DeadlineRepository(DbContext db) : Repository<Deadline>(db), IDeadlineRepository
{
    public async Task<IReadOnlyList<Deadline>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(d => matterId == null || d.MatterId == matterId)
            .OrderBy(d => d.DueDate).ToListAsync(ct);
}
