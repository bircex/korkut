using Korkut.Core;

namespace Korkut.Domain.Agent.Conversations;

/// <summary>
/// An assistant conversation ("session"): a titled thread of chat messages, optionally scoped to a
/// matter. Recency is <see cref="UpdatedAt"/>, bumped whenever a message is appended. A fresh
/// conversation starts with the placeholder title until its first message auto-titles it.
/// </summary>
public sealed class Conversation : Entity, ITenantOwned, IAggregateRoot
{
    public Conversation(Guid officeId, string title, Guid? matterId, Guid? createdBy, DateTimeOffset now)
        : base(NewId())
    {
        OfficeId = officeId;
        Title = title;
        MatterId = matterId;
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Conversation()
    {
    }

    public Guid OfficeId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public Guid? MatterId { get; private set; }

    /// <summary>The user who opened the conversation.</summary>
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Opens a copy of this conversation for a user (title marked as a copy); messages are cloned by the repository.</summary>
    public Conversation CloneFor(Guid createdBy, DateTimeOffset now)
        => new(OfficeId, Title + " (kopya)", MatterId, createdBy, now);
}
