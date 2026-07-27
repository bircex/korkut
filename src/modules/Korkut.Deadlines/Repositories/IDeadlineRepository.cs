using Korkut.Deadlines.Entities;
using Korkut.Core;

namespace Korkut.Deadlines;

/// <summary>Persistence port for deadlines.</summary>
internal interface IDeadlineRepository : IRepository<Deadline>
{
    /// <summary>The office's deadlines, optionally narrowed to one matter, earliest due first.</summary>
    Task<IReadOnlyList<Deadline>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
