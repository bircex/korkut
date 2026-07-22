using Korkut.Application.Ai;
using Korkut.Application.Knowledge;
using Korkut.Domain.Knowledge;

namespace Korkut.Application.Features.Knowledge;

/// <summary>Answers a retrieval query with the most relevant chunks for the caller's office.</summary>
public interface IRetrievalService
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(Guid officeId, string query, int topK, CancellationToken ct = default);
}

/// <summary>
/// Retrieval over the ports: embed the query with the SAME embedder used at ingestion (so the
/// query lands in the same vector space), then top-k similarity search. An empty/blank query
/// returns nothing rather than embedding it.
/// </summary>
public sealed class RetrievalService(IEmbedder embedder, IVectorStoreService storeService) : IRetrievalService
{
    public const int DefaultTopK = 6;

    public const int MaxTopK = 50;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(Guid officeId, string query, int topK, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var embedding = await embedder.EmbedAsync([query], ct);
        return await storeService.SearchAsync(officeId, embedding.Vectors[0], Math.Clamp(topK, 1, MaxTopK), ct);
    }
}
