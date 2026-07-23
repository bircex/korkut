using Korkut.Application.Deadlines;
using Korkut.Domain.Deadlines;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Deadlines;

/// <summary>EF/Postgres adapter for <see cref="IDeadlineRepository"/>.</summary>
public sealed class DeadlineRepository(KorkutDbContext db) : Repository<Deadline>(db), IDeadlineRepository
{
    public async Task<IReadOnlyList<Deadline>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(d => matterId == null || d.MatterId == matterId)
            .OrderBy(d => d.DueDate).ToListAsync(ct);
}
