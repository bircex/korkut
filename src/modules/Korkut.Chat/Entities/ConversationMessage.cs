using Korkut.Core;

namespace Korkut.Chat.Entities;

/// <summary>One turn in a conversation: who spoke ("user" | "assistant") and what they said.</summary>
public sealed class ConversationMessage : Entity, ITenantOwned, IAggregateRoot
{
    public ConversationMessage(
        Guid officeId, Guid conversationId, string role, string content, DateTimeOffset now, string? sourcesJson = null)
        : base(NewId())
    {
        OfficeId = officeId;
        ConversationId = conversationId;
        Role = role;
        Content = content;
        CreatedAt = now;
        SourcesJson = sourcesJson;
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

    /// <summary>
    /// Legal sources behind an assistant reply (snake_case <c>SourceRef[]</c> JSON), for the UI's
    /// sources drawer; null for user turns and answers that ran no legal search.
    /// </summary>
    public string? SourcesJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
