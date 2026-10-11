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

    void Add(TelegramBot bot);

    void Add(TelegramLink link);

    void Remove(TelegramBot bot);

    Task RemoveLinksAsync(Guid officeId, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
