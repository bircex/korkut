namespace Korkut.Knowledge;

/// <summary>Indexes a document's text into the vector store for retrieval.</summary>
public interface IIngestionService
{
    /// <summary>
    /// Chunk → embed → store one document's text under the current embedding version, replacing
    /// any prior chunks. Returns the number of chunks indexed; 0 clears the document from the index.
    /// </summary>
    Task<int> IngestAsync(Guid documentId, string text, CancellationToken ct = default);
}
