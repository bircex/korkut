using Korkut.Contract.Documents;
using Korkut.Core;
using Microsoft.Extensions.Logging;

namespace Korkut.Knowledge;

/// <summary>
/// <see cref="IKnowledgeSearchService"/> over the in-process <see cref="IRetrievalService"/>.
/// Retrieval is best-effort grounding: a missing embedding backend, a model outage or a database
/// error is logged and answered with no hits, never surfaced as a failed chat turn. Only passages
/// from documents the caller may see come back (<see cref="IDocumentAccess"/>): it asks the index
/// for more than it needs and keeps the visible ones.
/// </summary>
public sealed partial class GroundingSearchService(
    IRetrievalService retrieval, IDocumentAccess documents, IUnitOfWork unitOfWork, ICurrentUser currentUser,
    ILogger<GroundingSearchService> logger)
    : IKnowledgeSearchService
{
    private const int Overfetch = 5;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, Guid? matterId = null, CancellationToken ct = default)
    {
        try
        {
            var k = Math.Clamp(topK, 1, RetrievalService.MaxTopK);
            var office = currentUser.OfficeId;
            var visible = await unitOfWork.InTenantScopeAsync(office, () => documents.VisibleDocumentIdsAsync(office, matterId, ct), ct);
            if (visible is { Count: 0 })
            {
                return [];
            }

            if (visible is null)
            {
                return await retrieval.SearchAsync(query, k, ct);
            }

            var hits = await retrieval.SearchAsync(query, Math.Min(k * Overfetch, RetrievalService.MaxTopK), ct);
            return hits.Where(h => visible.Contains(h.DocumentId)).Take(k).ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ModelUnavailableException ex)
        {
            // No embedding backend is a configuration, not a fault: it would otherwise log a stack
            // trace on every chat turn of an install that simply has not set one up.
            NoEmbedder(logger, ex.Message);
            return [];
        }
        catch (Exception ex)
        {
            RetrievalFailed(logger, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Knowledge retrieval skipped: {Reason}")]
    private static partial void NoEmbedder(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Knowledge retrieval failed; answering ungrounded.")]
    private static partial void RetrievalFailed(ILogger logger, Exception ex);
}
