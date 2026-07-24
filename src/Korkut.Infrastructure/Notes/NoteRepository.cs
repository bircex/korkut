using Korkut.Application.Notes;
using Korkut.Domain.Notes;
using Korkut.Persistence;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Notes;

/// <summary>EF/Postgres adapter for <see cref="INoteRepository"/>.</summary>
public sealed class NoteRepository(KorkutDbContext db) : Repository<CommunicationNote>(db), INoteRepository
{
    public async Task<IReadOnlyList<CommunicationNote>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .Where(n => matterId == null || n.MatterId == matterId)
            .OrderByDescending(n => n.OccurredAt).ToListAsync(ct);
}
