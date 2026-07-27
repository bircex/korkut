using Korkut.Core;

namespace Korkut.Documents;

/// <summary>
/// Persistence port for document metadata. Listings show what still exists: a tombstoned document
/// is a marker, not a document.
/// </summary>
public interface IDocumentRepository : IRepository<Document>
{
    /// <summary>Active documents, newest first, optionally narrowed to one matter.</summary>
    Task<IReadOnlyList<Document>> ListAsync(Guid officeId, Guid? matterId, CancellationToken ct = default);

    /// <summary>An active document by id; null when it is missing or tombstoned.</summary>
    Task<Document?> FindActiveAsync(Guid officeId, Guid documentId, CancellationToken ct = default);
}
