using Korkut.Core;

namespace Korkut.Jobs.Entities;

/// <summary>
/// A unit of asynchronous work, recorded in the same transaction as the change that
/// produced it (the transactional-outbox pattern). It carries its own office, so a job
/// never crosses tenants, and an idempotency key, so a redelivery collapses into one
/// logical effect.
/// </summary>
/// <remarks>
/// The lifecycle lives here rather than in each store, because there are two of them (the
/// PostgreSQL one and the in-memory reference) and the rules must not drift apart.
/// Claiming a batch is the exception: the SQL store does it as one atomic statement with
/// FOR UPDATE SKIP LOCKED, which no aggregate method can express, so <see cref="IsLeasable"/>
/// states the same condition the SQL applies.
/// </remarks>
public sealed class OutboxMessage : Entity, ITenantOwned, IAggregateRoot
{
    public OutboxMessage(
        Guid officeId, string type, string payload, string idempotencyKey,
        DateTimeOffset availableAt, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Type = type;
        Payload = payload;
        IdempotencyKey = idempotencyKey;
        Status = JobStatus.Pending;
        AvailableAt = availableAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private OutboxMessage()
    {
    }

    /// <summary>Owning tenant. Every job is tenant-scoped and never crosses offices.</summary>
    public Guid OfficeId { get; private set; }

    /// <summary>Logical message type, used to resolve a handler.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Opaque serialized payload.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Dedup key: two messages with the same key in the same office run once.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public JobStatus Status { get; private set; } = JobStatus.Pending;

    public int Attempts { get; private set; }

    /// <summary>Earliest time the message may be claimed; this is what backs off a retry.</summary>
    public DateTimeOffset AvailableAt { get; private set; }

    /// <summary>Who holds the lease; null when nobody does.</summary>
    public string? LeaseOwner { get; private set; }

    /// <summary>When the lease expires — a crashed worker's message becomes claimable again.</summary>
    public DateTimeOffset? LeaseUntil { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Claimable: due and waiting, or leased by a worker that never came back.
    /// </summary>
    public bool IsLeasable(DateTimeOffset now)
        => (Status is JobStatus.Pending or JobStatus.Failed && AvailableAt <= now)
            || (Status is JobStatus.Leased && LeaseUntil is { } until && until <= now);

    public void Lease(string owner, DateTimeOffset leaseUntil, DateTimeOffset now)
    {
        Status = JobStatus.Leased;
        LeaseOwner = owner;
        LeaseUntil = leaseUntil;
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        Status = JobStatus.Completed;
        ReleaseLease();
        UpdatedAt = now;
    }

    /// <summary>The attempt failed; the message waits until <paramref name="availableAt"/> to be retried.</summary>
    public void Fail(int attempts, DateTimeOffset availableAt, string error)
    {
        Status = JobStatus.Failed;
        Attempts = attempts;
        AvailableAt = availableAt;
        LastError = error;
        ReleaseLease();
        UpdatedAt = availableAt;
    }

    /// <summary>Retries are exhausted: the message stops moving until an operator replays it.</summary>
    public void DeadLetter(string error, DateTimeOffset now)
    {
        Status = JobStatus.DeadLettered;
        LastError = error;
        ReleaseLease();
        UpdatedAt = now;
    }

    /// <summary>
    /// Operator recovery: puts a dead-lettered message back in the queue with a clean
    /// attempt count. False for anything that was not dead-lettered — replaying a message
    /// that is still in flight would run it twice.
    /// </summary>
    public bool Replay(DateTimeOffset now)
    {
        if (Status != JobStatus.DeadLettered)
        {
            return false;
        }

        Status = JobStatus.Pending;
        Attempts = 0;
        AvailableAt = now;
        LastError = null;
        ReleaseLease();
        UpdatedAt = now;
        return true;
    }

    private void ReleaseLease()
    {
        LeaseOwner = null;
        LeaseUntil = null;
    }
}
