namespace Korkut.Contract.Courts;

/// <summary>
/// Cross-module read for court reference data: resolves a court's CURRENT display name. Used e.g.
/// by Matters to snapshot the title at link time. Runs in the caller's already-open tenant scope.
/// </summary>
public interface ICourtLookup
{
    /// <summary>The court's current name, or null if no such court in the office.</summary>
    Task<string?> GetNameAsync(Guid officeId, Guid courtId, CancellationToken ct = default);
}
