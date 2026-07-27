
namespace Korkut.Knowledge;

/// <summary>
/// The retrieval port the AGENT depends on to ground a chat turn. Its adapter calls the internal
/// Korkut.Knowledge <c>/search</c> endpoint over HTTP, forwarding the caller's token so Knowledge
/// derives the office (RLS) itself — the office is never passed in the body. Retrieval logic can
/// evolve inside Knowledge without redeploying the Agent. When Knowledge is unconfigured or
/// unreachable the adapter returns an empty list, so chat degrades gracefully (ungrounded) rather
/// than failing.
/// </summary>
public interface IKnowledgeSearchService
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default);
}
