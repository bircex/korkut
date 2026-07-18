using Korkut.Application.Jobs;

namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>Persistent form of an outbox message (KDM-008 semantics, KDM-011 storage).</summary>
public sealed class OutboxEntity
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public JobStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseUntil { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Idempotency ledger row — one per completed (office, key) effect.</summary>
public sealed class InboxEntity
{
    public Guid OfficeId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}

/// <summary>Append-only job audit row.</summary>
public sealed class JobAuditEntity
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public Guid OfficeId { get; set; }

    public string Type { get; set; } = string.Empty;

    public JobAuditKind Kind { get; set; }

    public string? Detail { get; set; }

    public DateTimeOffset At { get; set; }
}
