namespace Korkut.Application.Jobs;

/// <summary>Lifecycle state of an outbox message.</summary>
public enum JobStatus
{
    Pending,
    Leased,
    Completed,
    Failed,
    DeadLettered,
}

/// <summary>
/// A unit of asynchronous work recorded transactionally alongside the state change
/// that produced it (the transactional-outbox pattern). Carries its own tenant
/// (<see cref="OfficeId"/>) and an <see cref="IdempotencyKey"/> so redeliveries
/// collapse to a single logical effect. The persistent EF-backed store arrives in
/// KDM-011; the in-memory store here proves the semantics.
/// </summary>
public sealed record OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>Owning tenant. Every job is tenant-scoped and never crosses offices.</summary>
    public required Guid OfficeId { get; init; }

    /// <summary>Logical message type used to resolve a handler.</summary>
    public required string Type { get; init; }

    /// <summary>Opaque serialized payload (no secrets/PII in plaintext beyond policy).</summary>
    public required string Payload { get; init; }

    /// <summary>Dedup key: two messages with the same key + office run once.</summary>
    public required string IdempotencyKey { get; init; }

    public JobStatus Status { get; init; } = JobStatus.Pending;

    public int Attempts { get; init; }

    /// <summary>Earliest time the message may be leased (drives retry backoff).</summary>
    public DateTimeOffset AvailableAt { get; init; }

    /// <summary>Lease holder token; null when not leased.</summary>
    public string? LeaseOwner { get; init; }

    /// <summary>Lease expiry; a crashed worker's lease expires and the message is re-leasable.</summary>
    public DateTimeOffset? LeaseUntil { get; init; }

    public string? LastError { get; init; }
}
