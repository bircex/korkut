namespace Korkut.Domain.Knowledge;

/// <summary>
/// One indexed passage of an office document (domain model). The vector itself lives in the
/// Knowledge DB (pgvector) and never leaves the infrastructure adapter; this model carries the
/// text + provenance. <see cref="EmbeddingVersion"/> stamps the vector space it was embedded in
/// (principle 9) — changing the embedder/chunker means a new version and a re-index, never a mix.
/// </summary>
public sealed record KnowledgeChunk(
    Guid Id, Guid OfficeId, Guid DocumentId, int Ordinal, string Text, string EmbeddingVersion, DateTimeOffset CreatedAt);

/// <summary>A chunk's text + its embedding, produced by ingestion before it is written to the store.</summary>
public sealed record ChunkEmbedding(int Ordinal, string Text, float[] Vector);

/// <summary>
/// A retrieval result: the matched chunk plus its similarity <see cref="Score"/> (higher = closer;
/// cosine similarity in [-1, 1], i.e. <c>1 - cosine_distance</c>). Returned to the Agent as grounded
/// context for the LLM.
/// </summary>
public sealed record SearchHit(Guid ChunkId, Guid DocumentId, int Ordinal, string Text, double Score);
