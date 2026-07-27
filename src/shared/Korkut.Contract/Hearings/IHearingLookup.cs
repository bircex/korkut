namespace Korkut.Contract.Hearings;

/// <summary>
/// Cross-module read for a matter's hearings (used by Matters to build the matter detail). Reads in
/// the caller's already-open tenant scope, so other modules never touch the Hearing entity type.
/// </summary>
public interface IHearingLookup
{
    /// <summary>A matter's hearings, earliest first.</summary>
    Task<IReadOnlyList<HearingResponse>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default);
}
