using Korkut.Core;
using Korkut.Persistence;
using Korkut.SavedDecisions.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.SavedDecisions.Repositories;

/// <summary>Persistence port for saved decisions; listings are most-recently-saved first.</summary>
internal interface ISavedDecisionRepository : IRepository<SavedDecision>
{
    Task<SavedDecision?> FindBySourceAsync(Guid officeId, string source, string externalId, CancellationToken ct = default);
}

/// <summary>EF/Postgres adapter for <see cref="ISavedDecisionRepository"/> over the ambient DbContext.</summary>
internal sealed class SavedDecisionRepository(DbContext db) : Repository<SavedDecision>(db), ISavedDecisionRepository
{
    public override async Task<IReadOnlyList<SavedDecision>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    public Task<SavedDecision?> FindBySourceAsync(Guid officeId, string source, string externalId, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking().SingleOrDefaultAsync(x => x.Source == source && x.ExternalId == externalId, ct);
}
