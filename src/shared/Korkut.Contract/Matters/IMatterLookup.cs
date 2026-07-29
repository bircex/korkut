namespace Korkut.Contract.Matters;

/// <summary>
/// Cross-module existence check for a matter (used by Hearings to validate the parent before
/// attaching). Reads in the caller's already-open tenant scope.
/// </summary>
public interface IMatterLookup
{
    /// <summary>Whether an active matter with this id exists in the office.</summary>
    Task<bool> ExistsAsync(Guid officeId, Guid matterId, CancellationToken ct = default);

    /// <summary>Whether a party with this id exists in the office (parties live inside the Matters module).</summary>
    Task<bool> PartyExistsAsync(Guid officeId, Guid partyId, CancellationToken ct = default);

    /// <summary>Matter id → title, for the requested ids. Absent ids simply drop out; empty input → empty map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> TitlesAsync(
        Guid officeId, IReadOnlyCollection<Guid> matterIds, CancellationToken ct = default);

    /// <summary>Active (non-deleted) matter counts grouped by status, for the dashboard.</summary>
    Task<IReadOnlyDictionary<string, int>> StatusCountsAsync(Guid officeId, CancellationToken ct = default);
}
