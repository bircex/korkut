namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A tenant-owned assistant conversation ("session"): a titled thread of chat messages,
/// optionally scoped to a matter. Ordering/recency is tracked by <see cref="UpdatedAt"/>.
/// </summary>
public sealed class ConversationEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid? MatterId { get; set; }

    /// <summary>The user who opened the conversation.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A single message in a conversation.</summary>
public sealed class ChatMessageEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid ConversationId { get; set; }

    /// <summary>"user" | "assistant".</summary>
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
