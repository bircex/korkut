
namespace Korkut.Knowledge;

/// <summary>
/// The retrieval port the agent depends on to ground a chat turn and to answer the search tool.
/// Unlike <see cref="IRetrievalService"/> it never throws: with no embedding backend configured, or
/// the index unreachable, it returns an empty list, so chat degrades to ungrounded rather than
/// failing the turn.
/// </summary>
public interface IKnowledgeSearchService
{
    /// <summary>Searches the documents the caller may see, only one matter's when <paramref name="matterId"/> is given.</summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, Guid? matterId = null, CancellationToken ct = default);
}
