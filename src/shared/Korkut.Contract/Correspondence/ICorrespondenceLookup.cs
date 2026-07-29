namespace Korkut.Contract.Correspondence;

/// <summary>
/// Cross-module read of the office's correspondence (evrak) counts by direction, so the dashboard
/// composes them by CALLING this rather than counting the table. Reads in the caller's already-open
/// tenant scope.
/// </summary>
public interface ICorrespondenceLookup
{
    /// <summary>Direction ("incoming"/"outgoing") → count.</summary>
    Task<IReadOnlyDictionary<string, int>> DirectionCountsAsync(Guid officeId, CancellationToken ct = default);
}
