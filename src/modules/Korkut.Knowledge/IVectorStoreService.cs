
namespace Korkut.Knowledge;

/// <summary>
/// The vector-store port the Knowledge service depends on (implemented over pgvector in
/// Infrastructure). Tenant context is set by the caller (RLS on <c>app.current_office</c>), so
/// the office is passed only to scope the write/read explicitly and to stamp rows. Re-indexing a
/// document is a full replace so stale chunks never linger under a new embedding version.
/// </summary>
public interface IVectorStoreService
{
    /// <summary>Replace ALL chunks of a document with the given embeddings under an embedding version.</summary>
    Task ReplaceDocumentAsync(
        Guid officeId, Guid documentId, IReadOnlyList<ChunkEmbedding> chunks, string embeddingVersion, CancellationToken ct = default);

    /// <summary>Top-k nearest chunks to the query vector (cosine), most-similar first.</summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(Guid officeId, float[] query, int topK, CancellationToken ct = default);

    /// <summary>Remove every chunk of a document (e.g. the source was deleted).</summary>
    Task DeleteDocumentAsync(Guid officeId, Guid documentId, CancellationToken ct = default);
}
