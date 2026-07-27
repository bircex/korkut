using Korkut.Core;
using Korkut.Notes.Entities;

namespace Korkut.Notes.Repositories;

/// <summary>Persistence port for communication notes.</summary>
internal interface INoteRepository : IRepository<CommunicationNote>
{
    /// <summary>The office's notes, optionally narrowed to one matter, most recent first.</summary>
    Task<IReadOnlyList<CommunicationNote>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);
}
