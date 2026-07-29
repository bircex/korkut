namespace Korkut.Jobs.Entities;

/// <summary>What happened to a job — recorded for operator audit and replay accountability.</summary>
public enum JobAuditKind
{
    Completed,
    DuplicateSkipped,
    Retried,
    DeadLettered,
    Replayed,
}
