using Korkut.Core;

namespace Korkut.Knowledge;

/// <summary>
/// Retrieval over the ports: embed the query with the SAME embedder used at ingestion (so the
/// query lands in the same vector space), then top-k similarity search in the caller's office. A
/// blank query returns nothing rather than embedding it.
/// </summary>
public sealed class RetrievalService(
    IEmbedder embedder,
    IVectorStoreService storeService,
    IKnowledgeUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IRetrievalService
{
    public const int DefaultTopK = 6;

    public const int MaxTopK = 50;

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<SearchHit>>([]);
        }

        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var embedding = await embedder.EmbedAsync([query], ct);
            return await storeService.SearchAsync(office, embedding.Vectors[0], Math.Clamp(topK, 1, MaxTopK), ct);
        }, ct);
    }
}
