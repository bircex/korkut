namespace Korkut.Contract.Tasks;

/// <summary>Open / done / overdue task tallies for the office (overdue = past due and still open).</summary>
public sealed record TaskCounts(int Open, int Done, int Overdue);

/// <summary>
/// Cross-module read of the office's task tallies, so the dashboard composes them by CALLING this
/// rather than counting the tasks table. Reads in the caller's already-open tenant scope.
/// </summary>
public interface ITaskLookup
{
    Task<TaskCounts> CountsAsync(Guid officeId, DateTimeOffset now, CancellationToken ct = default);
}
