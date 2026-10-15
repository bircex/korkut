using Korkut.Telegram.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Telegram.Repositories;

/// <summary>EF/Postgres adapter for <see cref="ITelegramRepository"/> over the ambient DbContext.</summary>
internal sealed class TelegramRepository(DbContext db) : ITelegramRepository
{
    public Task<TelegramBot?> FindBotAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<TelegramBot>().SingleOrDefaultAsync(b => b.OfficeId == officeId, ct);

    public Task<TelegramLink?> FindLinkAsync(Guid userId, CancellationToken ct = default)
        => db.Set<TelegramLink>().SingleOrDefaultAsync(l => l.UserId == userId, ct);

    public Task<TelegramLink?> FindLinkByChatAsync(long chatId, CancellationToken ct = default)
        => db.Set<TelegramLink>().SingleOrDefaultAsync(l => l.ChatId == chatId, ct);

    public Task<TelegramLink?> FindLinkByCodeAsync(string codeHash, CancellationToken ct = default)
        => db.Set<TelegramLink>().SingleOrDefaultAsync(l => l.CodeHash == codeHash, ct);

    public async Task<IReadOnlyList<TelegramLink>> ListLinkedAsync(Guid officeId, CancellationToken ct = default)
        => await db.Set<TelegramLink>().AsNoTracking()
            .Where(l => l.OfficeId == officeId && l.ChatId != null)
            .OrderBy(l => l.LinkedAt)
            .ToListAsync(ct);

    public Task<TelegramGroup?> FindGroupAsync(long chatId, CancellationToken ct = default)
        => db.Set<TelegramGroup>().SingleOrDefaultAsync(g => g.ChatId == chatId, ct);

    public async Task<IReadOnlyList<long>> GroupsForMatterAsync(Guid matterId, CancellationToken ct = default)
        => await db.Set<TelegramGroup>().AsNoTracking().Where(g => g.MatterId == matterId).Select(g => g.ChatId).ToListAsync(ct);

    public void Add(TelegramGroup group) => db.Set<TelegramGroup>().Add(group);

    public void Remove(TelegramGroup group) => db.Set<TelegramGroup>().Remove(group);

    public async Task<IReadOnlyList<long>> GroupMembersAsync(long chatId, CancellationToken ct = default)
        => await db.Set<TelegramGroupMember>().AsNoTracking()
            .Where(m => m.ChatId == chatId).Select(m => m.TelegramUserId).ToListAsync(ct);

    public async Task AddGroupMemberAsync(Guid officeId, long chatId, long telegramUserId, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!await db.Set<TelegramGroupMember>().AnyAsync(m => m.ChatId == chatId && m.TelegramUserId == telegramUserId, ct))
        {
            db.Set<TelegramGroupMember>().Add(new TelegramGroupMember(officeId, chatId, telegramUserId, now));
            await db.SaveChangesAsync(ct);
        }
    }

    public Task RemoveGroupMemberAsync(long chatId, long telegramUserId, CancellationToken ct = default)
        => db.Set<TelegramGroupMember>().Where(m => m.ChatId == chatId && m.TelegramUserId == telegramUserId).ExecuteDeleteAsync(ct);

    public void Add(TelegramBot bot) => db.Set<TelegramBot>().Add(bot);

    public void Add(TelegramLink link) => db.Set<TelegramLink>().Add(link);

    public void Remove(TelegramBot bot) => db.Set<TelegramBot>().Remove(bot);

    public Task RemoveLinksAsync(Guid officeId, CancellationToken ct = default)
        => db.Set<TelegramLink>().Where(l => l.OfficeId == officeId).ExecuteDeleteAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
