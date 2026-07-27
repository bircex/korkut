using Korkut.Domain.Knowledge;

namespace Korkut.Application.Knowledge;

/// <summary>Answers a retrieval query with the most relevant chunks for the caller's office.</summary>
public interface IRetrievalService
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct = default);
}
