using Korkut.Core;

namespace Korkut.Telegram.Entities;

/// <summary>
/// A Telegram group bound to one matter. The group talks to the assistant in one shared
/// conversation of that matter, and only while everyone in it may see the matter.
/// </summary>
public sealed class TelegramGroup : ITenantOwned, IAggregateRoot
{
    public TelegramGroup(Guid officeId, long chatId, Guid matterId, Guid linkedBy, Guid conversationId, DateTimeOffset now)
    {
        OfficeId = officeId;
        ChatId = chatId;
        Bind(matterId, linkedBy, conversationId, now);
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private TelegramGroup()
    {
    }

    public long ChatId { get; private set; }

    public Guid OfficeId { get; private set; }

    public Guid MatterId { get; private set; }

    public Guid LinkedBy { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    /// <summary>The matter conversation the group's messages go to.</summary>
    public Guid ConversationId { get; private set; }

    public void Bind(Guid matterId, Guid linkedBy, Guid conversationId, DateTimeOffset now)
    {
        MatterId = matterId;
        LinkedBy = linkedBy;
        ConversationId = conversationId;
        LinkedAt = now;
    }

    public void Continue(Guid conversationId) => ConversationId = conversationId;
}

/// <summary>
/// Someone the bot knows is in a group: seen writing, joining, or in a membership update. The Bot
/// API cannot list a group's members, so this is how the bot tells who is there.
/// </summary>
public sealed class TelegramGroupMember : ITenantOwned
{
    public TelegramGroupMember(Guid officeId, long chatId, long telegramUserId, DateTimeOffset now)
    {
        OfficeId = officeId;
        ChatId = chatId;
        TelegramUserId = telegramUserId;
        SeenAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private TelegramGroupMember()
    {
    }

    public long ChatId { get; private set; }

    public long TelegramUserId { get; private set; }

    public Guid OfficeId { get; private set; }

    public DateTimeOffset SeenAt { get; private set; }
}
