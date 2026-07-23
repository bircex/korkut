namespace Korkut.Domain.Knowledge;

/// <summary>
/// One indexed passage of an office document: its text and provenance. The embedding vector
/// itself never appears here — it is a pgvector value that lives only inside the store adapter,
/// so the Domain stays free of any persistence-technology type. <see cref="EmbeddingVersion"/>
/// stamps the vector space the passage was embedded in: changing the embedder or chunker means a
/// new version and a full re-index, never a mix of spaces.
/// </summary>
public sealed record KnowledgeChunk(
    Guid Id, Guid OfficeId, Guid DocumentId, int Ordinal, string Text, string EmbeddingVersion, DateTimeOffset CreatedAt);
