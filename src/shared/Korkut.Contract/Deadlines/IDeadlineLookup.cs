namespace Korkut.Contract.Deadlines;

/// <summary>
/// Cross-module read exposing an office's deadlines in a date window, so the calendar can
/// compose its unified view by CALLING this — never by joining the deadlines table. Reads in
/// the caller's already-open tenant scope (RLS keeps it to the office).
/// </summary>
public interface IDeadlineLookup
{
    /// <summary>Deadlines whose due date falls in [start, end] (either bound optional), optionally one matter.</summary>
    Task<IReadOnlyList<DeadlineResponse>> ListInRangeAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, Guid? matterId, CancellationToken ct = default);

    /// <summary>Open-deadline tallies for the dashboard: due in [today, until], and overdue (before today).</summary>
    Task<DeadlineCounts> CountsAsync(Guid officeId, DateOnly today, DateOnly until, CancellationToken ct = default);
}

/// <summary>Open deadlines upcoming within the window vs. already overdue.</summary>
public sealed record DeadlineCounts(int Upcoming, int Overdue);
