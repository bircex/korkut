using Korkut.Core;
using Korkut.Documents.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Documents.Services;

/// <summary>
/// The text the search index should hold for one version of a document: its extracted spans in
/// reading order, from the most recent parser run. Null when there is nothing to index any more:
/// the document is gone, no longer clean, or has moved on to a newer version (whose own index event
/// will follow).
/// </summary>
public sealed class DocumentTextService(DbContext db, IUnitOfWork unitOfWork)
{
    public Task<string?> ReadForIndexAsync(Guid officeId, Guid documentId, int version, CancellationToken ct = default)
        => unitOfWork.InTenantScopeAsync(officeId, async () =>
        {
            var document = await db.Set<Document>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == documentId, ct);
            if (document is not { IsDownloadable: true } || document.Version != version)
            {
                return null;
            }

            var spans = db.Set<DocumentExtraction>().AsNoTracking()
                .Where(x => x.OfficeId == officeId && x.DocumentId == documentId && x.DocumentVersion == version);
            var latestParser = await spans
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.ParserVersion)
                .FirstOrDefaultAsync(ct);
            if (latestParser is null)
            {
                return null;
            }

            var texts = await spans
                .Where(x => x.ParserVersion == latestParser)
                .OrderBy(x => x.Page).ThenBy(x => x.SpanIndex)
                .Select(x => x.Text)
                .ToListAsync(ct);
            return (string?)string.Join("\n\n", texts);
        }, ct);
}
