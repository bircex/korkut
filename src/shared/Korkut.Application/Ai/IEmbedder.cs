namespace Korkut.Application.Ai;

/// <summary>
/// Embedding output. Carries <see cref="ModelId"/> + <see cref="EmbeddingVersion"/> so every
/// vector can be stamped for index versioning (principle 9) — changing the embedder means a
/// new version and a re-index/cutover, never mixing incompatible vector spaces.
/// </summary>
public sealed record EmbeddingResult(
    IReadOnlyList<float[]> Vectors, string ModelId, string EmbeddingVersion, int Dimensions, int Tokens);

/// <summary>
/// The embedding port the Knowledge service depends on. Provider-agnostic, like
/// <see cref="IChatModel"/>; the real adapter lands with the Knowledge service.
/// </summary>
public interface IEmbedder
{
    Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
