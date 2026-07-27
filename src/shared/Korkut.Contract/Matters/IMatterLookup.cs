namespace Korkut.Contract.Matters;

/// <summary>
/// Cross-module existence check for a matter (used by Hearings to validate the parent before
/// attaching). Reads in the caller's already-open tenant scope.
/// </summary>
public interface IMatterLookup
{
    /// <summary>Whether an active matter with this id exists in the office.</summary>
    Task<bool> ExistsAsync(Guid officeId, Guid matterId, CancellationToken ct = default);
}
