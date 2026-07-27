using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Notes;

/// <summary>EF/Postgres adapter for <see cref="INoteRepository"/> over the ambient DbContext.</summary>
public sealed class NoteRepository(DbContext db) : Repository<CommunicationNote>(db), INoteRepository
{
    public async Task<IReadOnlyList<CommunicationNote>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(n => matterId == null || n.MatterId == matterId)
            .OrderByDescending(n => n.OccurredAt).ToListAsync(ct);
}
