using Korkut.Application.Abstractions.Knowledge;
using Korkut.Application.Ai;
using Korkut.Domain.Knowledge;

namespace Korkut.Application.Features.Knowledge;

/// <summary>Indexes a document's text into the vector store for retrieval.</summary>
public interface IIngestionService
{
    /// <summary>
    /// Chunk → embed → store one document's text under the current embedding version, replacing any
    /// prior chunks. Returns the number of chunks indexed (0 clears the document from the index).
    /// </summary>
    Task<int> IngestAsync(Guid officeId, Guid documentId, string text, CancellationToken ct = default);
}

/// <summary>
/// Ingestion pipeline over the ports: chunk the text, embed the chunks, and replace the document's
/// vectors — stamping the embedder's version so a later embedder change re-indexes under a new
/// space. PII masking (Korkut.Pii) will slot in between chunking and embedding when that library
/// lands (Phase 7); today the text is embedded as-is.
/// </summary>
public sealed class IngestionService(IChunker chunker, IEmbedder embedder, IVectorStore store) : IIngestionService
{
    public async Task<int> IngestAsync(Guid officeId, Guid documentId, string text, CancellationToken ct = default)
    {
        var chunks = chunker.Chunk(text);
        if (chunks.Count == 0)
        {
            await store.DeleteDocumentAsync(officeId, documentId, ct);
            return 0;
        }

        // PII masking (Korkut.Pii) belongs here, before the text leaves for the embedder — Phase 7.
        var embedding = await embedder.EmbedAsync(chunks, ct);
        var items = new List<ChunkEmbedding>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            items.Add(new ChunkEmbedding(i, chunks[i], embedding.Vectors[i]));
        }

        await store.ReplaceDocumentAsync(officeId, documentId, items, embedding.EmbeddingVersion, ct);
        return items.Count;
    }
}
