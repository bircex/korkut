namespace Korkut.Application.Jobs;

/// <summary>Ambient tenant scope for the currently-executing job.</summary>
public interface ITenantContext
{
    /// <summary>The office the current job runs under, or null outside a job.</summary>
    Guid? CurrentOfficeId { get; }

    /// <summary>Enter a tenant scope; disposing restores the previous one.</summary>
    IDisposable Enter(Guid officeId);
}

/// <summary>Handles one message type. Handlers MUST be idempotent and tenant-safe.</summary>
public interface IJobHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>Transactional outbox store. Lease/complete/fail/dead-letter are atomic per message.</summary>
public interface IOutboxStore
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>Atomically lease up to <paramref name="max"/> due, unleased messages.</summary>
    Task<IReadOnlyList<OutboxMessage>> LeaseBatchAsync(
        string owner, TimeSpan leaseDuration, DateTimeOffset now, int max, CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid id, int attempts, DateTimeOffset availableAt, string error, CancellationToken cancellationToken);

    Task DeadLetterAsync(Guid id, string error, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Requeue a dead-lettered message for reprocessing (operator recovery).</summary>
    Task<OutboxMessage?> ReplayAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Inbox / idempotency ledger: which (office, key) effects have completed.</summary>
public interface IInboxStore
{
    Task<bool> IsProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid officeId, string idempotencyKey, CancellationToken cancellationToken);
}

/// <summary>What happened to a job — recorded for operator audit / replay accountability.</summary>
public enum JobAuditKind
{
    Completed,
    DuplicateSkipped,
    Retried,
    DeadLettered,
    Replayed,
}

public sealed record JobAuditEvent(
    Guid MessageId, Guid OfficeId, string Type, JobAuditKind Kind, string? Detail, DateTimeOffset At);

/// <summary>Append-only audit sink for job outcomes and manual recovery.</summary>
public interface IJobAudit
{
    Task RecordAsync(JobAuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>Retry backoff policy and the dead-letter threshold.</summary>
public interface IRetryPolicy
{
    int MaxAttempts { get; }

    TimeSpan BackoffFor(int attempt);
}
