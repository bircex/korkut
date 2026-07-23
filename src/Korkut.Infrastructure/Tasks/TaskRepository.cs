using Korkut.Application.Tasks;
using Korkut.Domain.Tasks;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Tasks;

/// <summary>EF/Postgres adapter for <see cref="ITaskRepository"/>.</summary>
public sealed class TaskRepository(KorkutDbContext db) : Repository<WorkTask>(db), ITaskRepository
{
    public async Task<IReadOnlyList<WorkTask>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(t => matterId == null || t.MatterId == matterId)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
}
