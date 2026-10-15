using Korkut.Telegram.Entities;

namespace Korkut.Telegram.Repositories;

/// <summary>Persistence port for the office bot and members' links; reads run in the caller's tenant scope.</summary>
public interface ITelegramRepository
{
    /// <summary>Tracked, for mutation.</summary>
    Task<TelegramBot?> FindBotAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracked, for mutation.</summary>
    Task<TelegramLink?> FindLinkAsync(Guid userId, CancellationToken ct = default);

    Task<TelegramLink?> FindLinkByChatAsync(long chatId, CancellationToken ct = default);

    Task<TelegramLink?> FindLinkByCodeAsync(string codeHash, CancellationToken ct = default);

    Task<IReadOnlyList<TelegramLink>> ListLinkedAsync(Guid officeId, CancellationToken ct = default);

    /// <summary>Tracked, for mutation.</summary>
    Task<TelegramGroup?> FindGroupAsync(long chatId, CancellationToken ct = default);

    void Add(TelegramGroup group);

    void Remove(TelegramGroup group);

    /// <summary>The people the bot knows are in a group.</summary>
    Task<IReadOnlyList<long>> GroupMembersAsync(long chatId, CancellationToken ct = default);

    /// <summary>Records that someone is in a group (idempotent).</summary>
    Task AddGroupMemberAsync(Guid officeId, long chatId, long telegramUserId, DateTimeOffset now, CancellationToken ct = default);

    Task RemoveGroupMemberAsync(long chatId, long telegramUserId, CancellationToken ct = default);

    void Add(TelegramBot bot);

    void Add(TelegramLink link);

    void Remove(TelegramBot bot);

    Task RemoveLinksAsync(Guid officeId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
