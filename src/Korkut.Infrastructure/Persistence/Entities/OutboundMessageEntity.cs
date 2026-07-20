namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A tenant-owned outbound message awaiting human review before it is sent (the assistant
/// drafts it; a person confirms or cancels). Distinct from the transactional job
/// <c>outbox_messages</c> table — this is the user-facing review queue with
/// recipient/subject/body content.
/// </summary>
public sealed class OutboundMessageEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>"email" | "telegram" | "whatsapp".</summary>
    public string Kind { get; set; } = "email";

    public string ToAddr { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset? SendAt { get; set; }

    /// <summary>"draft" | "queued" | "sent" | "failed" | "cancelled".</summary>
    public string Status { get; set; } = "draft";

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
