using Korkut.Core;

namespace Korkut.Telegram.Entities;

/// <summary>
/// One member's link to the office bot. It starts as a pending one-time code (only its hash is
/// kept) and becomes a link when the member sends that code to the bot from their own chat. It also
/// remembers which Korkut conversation the chat is continuing. One row per member.
/// </summary>
public sealed class TelegramLink : ITenantOwned, IAggregateRoot
{
    public TelegramLink(Guid officeId, Guid userId)
    {
        OfficeId = officeId;
        UserId = userId;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private TelegramLink()
    {
    }

    public Guid UserId { get; private set; }

    public Guid OfficeId { get; private set; }

    public long? ChatId { get; private set; }

    public string? TelegramUsername { get; private set; }

    public DateTimeOffset? LinkedAt { get; private set; }

    public string? CodeHash { get; private set; }

    public DateTimeOffset? CodeExpiresAt { get; private set; }

    /// <summary>The Korkut conversation messages from this chat go to; a new one starts when null.</summary>
    public Guid? ConversationId { get; private set; }

    public bool IsLinked => ChatId is not null;

    public void IssueCode(string codeHash, DateTimeOffset expiresAt)
    {
        CodeHash = codeHash;
        CodeExpiresAt = expiresAt;
    }

    public void Link(long chatId, string? telegramUsername, DateTimeOffset now)
    {
        ChatId = chatId;
        TelegramUsername = telegramUsername;
        LinkedAt = now;
        CodeHash = null;
        CodeExpiresAt = null;
        ConversationId = null;
    }

    public void Unlink()
    {
        ChatId = null;
        TelegramUsername = null;
        LinkedAt = null;
        CodeHash = null;
        CodeExpiresAt = null;
        ConversationId = null;
    }

    public void Continue(Guid? conversationId) => ConversationId = conversationId;
}
