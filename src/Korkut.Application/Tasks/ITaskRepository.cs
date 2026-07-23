using Korkut.Application.Common;
using Korkut.Domain.Tasks;

namespace Korkut.Application.Tasks;

/// <summary>Persistence port for office tasks.</summary>
public interface ITaskRepository : IRepository<WorkTask>
{
    /// <summary>The office's tasks, optionally narrowed to one matter, newest first.</summary>
    Task<IReadOnlyList<WorkTask>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
