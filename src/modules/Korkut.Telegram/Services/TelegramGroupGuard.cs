using Korkut.Contract.Matters;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Telegram.Repositories;

namespace Korkut.Telegram.Services;

/// <summary>
/// Whether every person in a Telegram group may see a matter: the bot must know each of them (the
/// group's member count, less the bot, matches whom it has seen), and each must be a linked member
/// on the matter's team or one who sees every matter. Anything the bot says in a group about a
/// matter, an answer or a reminder, goes out only when this holds.
/// </summary>
public sealed class TelegramGroupGuard(
    ITelegramRepository repository, ITelegramApi telegram, IUnitOfWork unitOfWork, IUserLookup users,
    IRolePermissions rolePermissions, IMatterLookup matterLookup)
{
    public async Task<bool> EveryoneMaySeeAsync(Guid office, string token, long chatId, Guid matterId, CancellationToken ct = default)
    {
        var botId = TelegramGroupHandler.BotId(token);
        var count = await telegram.GetChatMemberCountAsync(token, chatId, ct);
        return await unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var known = (await repository.GroupMembersAsync(chatId, ct)).Where(id => id != botId).ToList();
            if (count - 1 != known.Count)
            {
                return false; // someone the bot has not seen, or someone gone it missed
            }

            var team = (await matterLookup.TeamsAsync(office, ct)).GetValueOrDefault(matterId) ?? [];
            foreach (var telegramUserId in known)
            {
                var link = await repository.FindLinkByChatAsync(telegramUserId, ct);
                var role = link is null ? null : await users.RoleAsync(office, link.UserId, ct);
                if (link is null || role is null)
                {
                    return false;
                }

                var granted = await rolePermissions.ForRoleAsync(office, role, ct);
                if (!granted.Contains(Permissions.MattersRead)
                    || !(granted.Contains(Permissions.MattersViewAll) || team.Contains(link.UserId)))
                {
                    return false;
                }
            }

            return true;
        }, ct);
    }
}
