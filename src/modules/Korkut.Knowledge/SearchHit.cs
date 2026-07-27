namespace Korkut.Knowledge;

/// <summary>
/// A retrieval result: the matched chunk and its similarity <see cref="Score"/> (higher is closer;
/// cosine similarity in [-1, 1], i.e. <c>1 - cosine_distance</c>). Returned to the Agent as
/// grounded context for the LLM.
/// </summary>
public sealed record SearchHit(Guid ChunkId, Guid DocumentId, int Ordinal, string Text, double Score);
