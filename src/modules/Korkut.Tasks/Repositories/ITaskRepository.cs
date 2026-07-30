using Korkut.Core;
using Korkut.Tasks.Entities;

namespace Korkut.Tasks.Repositories;

/// <summary>Persistence port for office agenda records.</summary>
internal interface ITaskRepository : IRepository<WorkTask>
{
    /// <summary>The office's records, optionally narrowed to one matter / status / category, newest first.</summary>
    Task<IReadOnlyList<WorkTask>> ListAsync(
        Guid officeId, Guid? matterId, string? status, string? category, CancellationToken ct = default);
}
