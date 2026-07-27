namespace Korkut.Domain.Knowledge;

/// <summary>A chunk's text and its embedding, produced by ingestion before it is written to the store.</summary>
public sealed record ChunkEmbedding(int Ordinal, string Text, float[] Vector);
