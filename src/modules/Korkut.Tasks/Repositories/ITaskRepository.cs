using Korkut.Tasks.Entities;
using Korkut.Core;

namespace Korkut.Tasks;

/// <summary>Persistence port for office tasks.</summary>
internal interface ITaskRepository : IRepository<WorkTask>
{
    /// <summary>The office's tasks, optionally narrowed to one matter, newest first.</summary>
    Task<IReadOnlyList<WorkTask>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
