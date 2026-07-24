using Korkut.Core;
using Korkut.Domain.Notes;

namespace Korkut.Application.Notes;

/// <summary>Persistence port for communication notes.</summary>
public interface INoteRepository : IRepository<CommunicationNote>
{
    /// <summary>The office's notes, optionally narrowed to one matter, most recent first.</summary>
    Task<IReadOnlyList<CommunicationNote>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
