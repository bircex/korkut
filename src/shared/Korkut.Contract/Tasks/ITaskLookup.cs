namespace Korkut.Contract.Tasks;

/// <summary>Open / done / overdue tallies for the office's görev-category records
/// (overdue = past due and still open).</summary>
public sealed record TaskCounts(int Open, int Done, int Overdue);

/// <summary>An agenda record as the calendar needs it: when it is, category, and the links/labels
/// the calendar enriches. Only scheduled records (with a <see cref="StartAt"/>) surface.</summary>
public sealed record TaskCalendarRow(
    Guid Id, string Category, string? Type, string Title, DateTimeOffset StartAt, DateTimeOffset? EndAt, bool AllDay,
    Guid? MatterId, Guid? ClientId, Guid? AssigneeId, string? Location, string Status, string? Description,
    string? Rrule, int? RemindBeforeMin, string ReminderStatus);

/// <summary>
/// Cross-module read of the office's agenda records. The dashboard tallies görev-category records by
/// CALLING <see cref="CountsAsync"/>, and the calendar composes its unified view from
/// <see cref="ListInRangeAsync"/> — never by touching the WorkTask type or the tasks table directly.
/// Reads in the caller's already-open tenant scope.
/// </summary>
public interface ITaskLookup
{
    /// <summary>Görev-category task tallies (hatırlatma/randevu are excluded — those are not to-dos).</summary>
    Task<TaskCounts> CountsAsync(Guid officeId, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Scheduled records whose start falls in [start, end] (either bound optional), optionally
    /// one matter — the lightweight rows the calendar composes from (it enriches the names itself).</summary>
    Task<IReadOnlyList<TaskCalendarRow>> ListInRangeAsync(
        Guid officeId, DateTimeOffset? start, DateTimeOffset? end, Guid? matterId, CancellationToken ct = default);
}
