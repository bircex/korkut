using Korkut.Documents.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Documents.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="IDocumentRepository"/> over the ambient DbContext. Tombstoned
/// rows are filtered out of every read: they exist as deletion markers, not as documents.
/// </summary>
internal sealed class DocumentRepository(DbContext db) : Repository<Document>(db), IDocumentRepository
{
    public async Task<IReadOnlyList<Document>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Active(officeId)
            .Where(d => matterId == null || d.MatterId == matterId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public Task<Document?> FindActiveAsync(Guid officeId, Guid documentId, CancellationToken ct = default)
        => Active(officeId).SingleOrDefaultAsync(d => d.Id == documentId, ct);

    public async Task<IReadOnlyList<DocumentRelation>> RelationsAsync(Guid officeId, CancellationToken ct = default)
        => await Db.Set<DocumentRelation>().AsNoTracking().Where(r => r.OfficeId == officeId).ToListAsync(ct);

    private IQueryable<Document> Active(Guid officeId)
        => Scoped(officeId).AsNoTracking().Where(d => d.Status != DocumentStatus.Tombstoned);
}
