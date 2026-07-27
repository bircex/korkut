namespace Korkut.Knowledge;

/// <summary>
/// Splits a document's plain text into overlapping passages for embedding. Overlap preserves
/// context across boundaries so a passage isn't cut mid-thought. The strategy (size/overlap) is
/// part of the index contract: changing it changes the vector space, so it travels with the
/// embedding version.
/// </summary>
public interface IChunkerService
{
    IReadOnlyList<string> Chunk(string text);
}
