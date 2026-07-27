namespace Korkut.Domain.Jobs;

/// <summary>Where a queued job is in its life: waiting, taken, done, or given up on.</summary>
public enum JobStatus
{
    Pending,
    Leased,
    Completed,
    Failed,
    DeadLettered,
}
