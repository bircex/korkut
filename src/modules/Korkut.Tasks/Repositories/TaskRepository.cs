using Korkut.Persistence;
using Korkut.Tasks.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Tasks.Repositories;

/// <summary>EF/Postgres adapter for <see cref="ITaskRepository"/> over the ambient DbContext.</summary>
internal sealed class TaskRepository(DbContext db) : Repository<WorkTask>(db), ITaskRepository
{
    public async Task<IReadOnlyList<WorkTask>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(t => matterId == null || t.MatterId == matterId)
            .OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
}
