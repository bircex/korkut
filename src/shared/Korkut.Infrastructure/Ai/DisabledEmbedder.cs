using Korkut.Core;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// Fallback embedder used by the Knowledge host when no embedding endpoint is configured — every
/// call reports unavailable (503). Stays in the shared Infrastructure layer alongside the real
/// <see cref="OpenAiCompatibleEmbedder"/>; the chat-model side moved to the Agent host.
/// </summary>
public sealed class DisabledEmbedder : IEmbedder
{
    public Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
        throw new ModelUnavailableException("Embedding sağlayıcısı yapılandırılmamış.");
}
