using Korkut.Application.Documents;
using Korkut.Domain.Documents;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Documents;

/// <summary>
/// EF/Postgres adapter for <see cref="IDocumentRepository"/>. Tombstoned rows are filtered
/// out of every read: they exist as deletion markers, not as documents.
/// </summary>
public sealed class DocumentRepository(KorkutDbContext db) : Repository<Document>(db), IDocumentRepository
{
    public async Task<IReadOnlyList<Document>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default)
        => await Active(officeId)
            .Where(d => matterId == null || d.MatterId == matterId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public Task<Document?> FindActiveAsync(Guid officeId, Guid documentId, CancellationToken ct = default)
        => Active(officeId).SingleOrDefaultAsync(d => d.Id == documentId, ct);

    private IQueryable<Document> Active(Guid officeId)
        => Scoped(officeId).AsNoTracking().Where(d => d.Status != DocumentStatus.Tombstoned);
}
