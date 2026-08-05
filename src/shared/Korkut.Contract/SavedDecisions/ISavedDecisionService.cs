namespace Korkut.Contract.SavedDecisions;

public sealed record SaveDecisionRequest(
    string Source, string ExternalId, string Title, string? Date, string? CaseNumber,
    string? DecisionNumber, string? Court, string? Chamber, string? Url, string? Snippet);

public sealed record SavedDecisionResponse(
    Guid Id, string Source, string ExternalId, string Title, string? Date, string? CaseNumber,
    string? DecisionNumber, string? Court, string? Chamber, string? Url, string? Snippet,
    DateTimeOffset CreatedAt);

/// <summary>
/// The office's saved decisions ("Kararlarım") — a shared bookmark list over the public
/// databases ILawClientsSearchService/ILawClientsDocumentService search. Every call runs in the
/// caller's tenant scope and RETURNS a <see cref="Result"/>.
/// </summary>
public interface ISavedDecisionService
{
    Task<Result<IReadOnlyList<SavedDecisionResponse>>> ListAsync(CancellationToken ct = default);

    /// <summary>Idempotent: saving the same source+external_id again returns the existing record.</summary>
    Task<Result<SavedDecisionResponse>> SaveAsync(SaveDecisionRequest request, CancellationToken ct = default);

    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
}
