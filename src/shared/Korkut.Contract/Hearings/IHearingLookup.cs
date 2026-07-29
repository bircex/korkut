namespace Korkut.Contract.Hearings;

/// <summary>
/// Cross-module read for a matter's hearings (used by Matters to build the matter detail). Reads in
/// the caller's already-open tenant scope, so other modules never touch the Hearing entity type.
/// </summary>
public interface IHearingLookup
{
    /// <summary>A matter's hearings, earliest first.</summary>
    Task<IReadOnlyList<HearingResponse>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default);

    /// <summary>
    /// Hearings scheduled in [start, end] (either bound optional) across the office, optionally one
    /// matter — the lightweight rows the calendar composes from (it enriches the matter title itself).
    /// </summary>
    Task<IReadOnlyList<HearingCalendarRow>> ListInRangeAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, Guid? matterId, CancellationToken ct = default);
}

/// <summary>A hearing as the calendar needs it: when it is, and the matter it belongs to.</summary>
public sealed record HearingCalendarRow(Guid Id, Guid MatterId, DateTimeOffset ScheduledAt);
