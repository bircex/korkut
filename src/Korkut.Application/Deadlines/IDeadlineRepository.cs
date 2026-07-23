using Korkut.Application.Common;
using Korkut.Domain.Deadlines;

namespace Korkut.Application.Deadlines;

/// <summary>Persistence port for deadlines.</summary>
public interface IDeadlineRepository : IRepository<Deadline>
{
    /// <summary>The office's deadlines, optionally narrowed to one matter, earliest due first.</summary>
    Task<IReadOnlyList<Deadline>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
