using Korkut.Core;
using Korkut.Tasks.Entities;

namespace Korkut.Tasks.Repositories;

/// <summary>Persistence port for office tasks.</summary>
internal interface ITaskRepository : IRepository<WorkTask>
{
    /// <summary>The office's tasks, optionally narrowed to one matter, newest first.</summary>
    Task<IReadOnlyList<WorkTask>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
