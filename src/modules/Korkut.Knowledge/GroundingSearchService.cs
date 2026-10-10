using Korkut.Core;
using Microsoft.Extensions.Logging;

namespace Korkut.Knowledge;

/// <summary>
/// <see cref="IKnowledgeSearchService"/> over the in-process <see cref="IRetrievalService"/>.
/// Retrieval is best-effort grounding: a missing embedding backend, a model outage or a database
/// error is logged and answered with no hits, never surfaced as a failed chat turn.
/// </summary>
public sealed partial class GroundingSearchService(IRetrievalService retrieval, ILogger<GroundingSearchService> logger)
    : IKnowledgeSearchService
{
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default)
    {
        try
        {
            return await retrieval.SearchAsync(query, Math.Clamp(topK, 1, RetrievalService.MaxTopK), ct);
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
