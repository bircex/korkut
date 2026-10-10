namespace Korkut.Knowledge;

/// <summary>Indexes a document's text into the vector store for retrieval.</summary>
public interface IIngestionService
{
    /// <summary>
    /// Chunk → embed → store one of <paramref name="officeId"/>'s documents under the current
    /// embedding version, replacing any prior chunks. Returns the number of chunks indexed; 0
    /// clears the document from the index. The office is explicit because indexing runs in the
    /// background, where there is no signed-in caller to take it from.
    /// </summary>
    Task<int> IngestAsync(Guid officeId, Guid documentId, string text, CancellationToken ct = default);
}
