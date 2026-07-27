using Korkut.Core;

namespace Korkut.Chat.Entities;

/// <summary>One turn in a conversation: who spoke ("user" | "assistant") and what they said.</summary>
public sealed class ConversationMessage : Entity, ITenantOwned, IAggregateRoot
{
    public ConversationMessage(Guid officeId, Guid conversationId, string role, string content, DateTimeOffset now)
        : base(NewId())
    {
        OfficeId = officeId;
        ConversationId = conversationId;
        Role = role;
        Content = content;
        CreatedAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private ConversationMessage()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid ConversationId { get; private set; }

    /// <summary>"user" | "assistant".</summary>
    public string Role { get; private set; } = "user";

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
