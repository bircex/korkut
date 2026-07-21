using Korkut.Application.Abstractions.Knowledge;
using Korkut.Application.Ai;
using Korkut.Domain.Knowledge;
using Korkut.Pii;

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
/// Ingestion pipeline over the ports: PII-mask the text, chunk it, embed the chunks, and replace
/// the document's vectors — stamping the embedder's version so a later embedder change re-indexes
/// under a new space. The vector index therefore never holds raw PII (TCKN, phone, e-mail, IBAN):
/// masking happens FIRST, so both the stored chunk text and its embedding are over masked content.
/// </summary>
public sealed class IngestionService(IChunker chunker, IEmbedder embedder, IVectorStore store, IPiiRedactor redactor) : IIngestionService
{
    public async Task<int> IngestAsync(Guid officeId, Guid documentId, string text, CancellationToken ct = default)
    {
        // Mask PII before anything is chunked, embedded, or stored — the index must never hold it.
        var masked = redactor.Redact(text).Text;
        var chunks = chunker.Chunk(masked);
        if (chunks.Count == 0)
        {
            await store.DeleteDocumentAsync(officeId, documentId, ct);
            return 0;
        }

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
