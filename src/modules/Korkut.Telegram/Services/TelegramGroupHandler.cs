using Korkut.Contract.Agent.Chat;
using Korkut.Contract.Matters;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Telegram.Entities;
using Korkut.Telegram.Repositories;
using Microsoft.Extensions.Logging;

namespace Korkut.Telegram.Services;

/// <summary>
/// The bot in a Telegram group. A member who may manage a matter's team binds the group to that
/// matter (/baglan); from then on the group talks to the assistant in one shared conversation of the
/// matter, and only while everyone in the group may see it: the one asking must be a linked member
/// on the matter, and so must every other person in the group. The Bot API cannot list a group's
/// members, so the bot keeps track of who it has seen join, leave or write, and compares that with
/// the group's member count; anyone it does not know about means nothing is shared. Client contact
/// and identity details are never given in a group, whoever asks.
/// </summary>
public sealed partial class TelegramGroupHandler(
    ITelegramRepository repository, ITelegramApi telegram, IUnitOfWork unitOfWork, IUserLookup users,
    IRolePermissions rolePermissions, ActingUser acting, ISessionAppService sessions, ISessionChatService chat,
    IMatterService matters, IMatterLookup matterLookup, IClock clock, ILogger<TelegramGroupHandler> logger)
{
    internal const string NotAllowed = "Bu dosya için yetkiniz yok.";

    internal const string StrangersPresent =
        "Bu grupta dosyaya yetkisi olmayan kişiler olduğu için içerik paylaşamıyorum.";

    private const string Help =
        "Bu grup bir dosyaya bağlanınca dosya hakkındaki sorularınızı burada yanıtlarım. Bana bir komutla, "
        + "adımı anarak ya da mesajımı yanıtlayarak yazın.\n\n"
        + "/baglan <ad ya da esas no> grubu bir dosyaya bağlar (ekip yönetme yetkisi gerekir), /ayir bağı kaldırır, "
        + "/yeni grupta yeni bir sohbet başlatır.\n\n"
        + "Gruptaki herkes Korkut'a bağlı ve dosyanın ekibinde olmalıdır; gruba katılanları görebilmem için beni yönetici yapın. "
        + "Müvekkil iletişim ve kimlik bilgilerini grupta paylaşmam.";

    private const string Failed = "Bir sorun oluştu ve yanıt veremedim. Biraz sonra yeniden deneyin.";

    /// <summary>Someone joined, left or changed standing in a group.</summary>
    public Task MembershipAsync(Guid office, string token, TelegramChatMemberUpdated change, CancellationToken ct = default)
    {
        var who = change.NewChatMember.User;
        if (who.Id == BotId(token))
        {
            return Task.CompletedTask;
        }

        return unitOfWork.InTenantScopeAsync(office, async () =>
        {
            if (change.NewChatMember.InGroup)
            {
                await repository.AddGroupMemberAsync(office, change.Chat.Id, who.Id, clock.UtcNow, ct);
            }
            else
            {
                await repository.RemoveGroupMemberAsync(change.Chat.Id, who.Id, ct);
            }

            return 0;
        }, ct);
    }

    public async Task HandleAsync(Guid office, string token, TelegramMessage message, CancellationToken ct = default)
    {
        var chatId = message.Chat.Id;
        var botId = BotId(token);
        await TrackAsync(office, chatId, botId, message, ct);
        if (message.NewChatMembers is not null || message.LeftChatMember is not null)
        {
            return; // a join or leave notice, not something to answer
        }

        var text = message.Text?.Trim() ?? string.Empty;
        var (command, argument) = TelegramUpdateHandler.ParseCommand(text);
        var username = await unitOfWork.InTenantScopeAsync(office, async () => (await repository.FindBotAsync(office, ct))?.BotUsername, ct);
        var mention = username is null ? null : $"@{username}";
        var addressed = command.Length > 0
            || (mention is not null && text.Contains(mention, StringComparison.OrdinalIgnoreCase))
            || message.ReplyToMessage?.From?.Id == botId;
        if (!addressed || message.From is not { IsBot: false } sender)
        {
            return; // the group talks among itself; the bot answers only when spoken to
        }

        if (command is "/start" or "/yardim" or "/help")
        {
            await ReplyAsync(token, chatId, Help, ct);
            return;
        }

        var link = await unitOfWork.InTenantScopeAsync(office, () => repository.FindLinkByChatAsync(sender.Id, ct), ct);
        var role = link is null ? null : await unitOfWork.InTenantScopeAsync(office, () => users.RoleAsync(office, link.UserId, ct), ct);
        if (link is null || role is null)
        {
            await ReplyAsync(token, chatId, NotAllowed, ct);
            return;
        }

        // The member speaks here without client contact or identity details, whatever their role.
        var permissions = InGroup(await rolePermissions.ForRoleAsync(office, role, ct));
        acting.Set(office, link.UserId, role, permissions);
        var group = await unitOfWork.InTenantScopeAsync(office, () => repository.FindGroupAsync(chatId, ct), ct);

        switch (command)
        {
            case "/baglan":
                await ReplyAsync(token, chatId, await BindAsync(office, link.UserId, permissions, chatId, group, argument, ct), ct);
                return;
            case "/ayir":
                await ReplyAsync(token, chatId, await UnbindAsync(office, permissions, group, ct), ct);
                return;
        }

        if (group is null)
        {
            await ReplyAsync(token, chatId,
                "Bu grup henüz bir dosyaya bağlı değil. Ekip yönetme yetkisi olan bir üye /baglan <ad ya da esas no> ile bağlayabilir.", ct);
            return;
        }

        if (!await SeesAsync(group.MatterId, ct))
        {
            await ReplyAsync(token, chatId, NotAllowed, ct);
            return;
        }

        if (!await EveryoneMaySeeAsync(office, token, chatId, botId, group.MatterId, ct))
        {
            await ReplyAsync(token, chatId, StrangersPresent, ct);
            return;
        }

        if (command is "/yeni" or "/new")
        {
            await ContinueInNewAsync(office, group, ct);
            await ReplyAsync(token, chatId, "Grupta yeni bir sohbet başladı. Ne yapmamı istersiniz?", ct);
            return;
        }

        var question = mention is null ? text : text.Replace(mention, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (question.Length == 0)
        {
            await ReplyAsync(token, chatId, "Buradayım. Dosyayla ilgili sorunuzu yazın.", ct);
            return;
        }

        await AnswerAsync(office, token, chatId, link.UserId, permissions, group, question, ct);
    }

    private async Task AnswerAsync(
        Guid office, string token, long chatId, Guid userId, IReadOnlySet<string> permissions, TelegramGroup group, string question,
        CancellationToken ct)
    {
        var reply = new StreamingReply(telegram, token, chatId);
        try
        {
            var outcome = await chat.ChatStreamAsync(
                office, userId, group.ConversationId, question, permissions, _ => Task.CompletedTask, d => reply.AppendAsync(d, ct), ct: ct);
            if (outcome is null)
            {
                // The group's conversation was deleted in the app: carry on in a new one.
                await ContinueInNewAsync(office, group, ct);
                outcome = await chat.ChatStreamAsync(
                    office, userId, group.ConversationId, question, permissions, _ => Task.CompletedTask, d => reply.AppendAsync(d, ct), ct: ct);
            }

            await reply.FinishAsync(string.IsNullOrWhiteSpace(outcome?.Reply) ? Failed : outcome.Reply, ct);
        }
        catch (Exception ex) when (ex is ModelUnavailableException or BudgetExceededException or TelegramApiException or ForbiddenException)
        {
            await reply.FinishAsync(ex.Message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TurnFailed(logger, ex);
            await reply.FinishAsync(Failed, ct);
        }
    }

    /// <summary>/baglan: bind the group to one of the member's matters, in a new conversation there.</summary>
    private async Task<string> BindAsync(
        Guid office, Guid userId, IReadOnlySet<string> permissions, long chatId, TelegramGroup? group, string query, CancellationToken ct)
    {
        if (!permissions.Contains(Permissions.MattersManageTeam))
        {
            return "Grubu bir dosyaya bağlamak için ekip yönetme yetkisi gerekir.";
        }

        if (group is not null && !await SeesAsync(group.MatterId, ct))
        {
            return NotAllowed; // the group is someone else's matter's; it is not theirs to move
        }

        if (query.Length == 0)
        {
            return "Hangi dosya? Örnek: /baglan Kira davası ya da /baglan 2026/123.";
        }

        var found = TelegramUpdateHandler.Find((await matters.ListAsync(ct)).Value ?? [], query);
        if (found.Count == 0)
        {
            return "Bu dosya bulunamadı veya yetkiniz yok.";
        }

        if (found.Count > 1)
        {
            var options = string.Join('\n', found.Take(30).Select(m => $"• {TelegramUpdateHandler.Describe(m)}"));
            return $"Birden çok dosya eşleşti:\n{options}\n\nTam adıyla ya da esas numarasıyla yazın.";
        }

        var matter = found[0];
        var conversation = await sessions.CreateAsync(matter.Id, ct);
        await unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var existing = await repository.FindGroupAsync(chatId, ct);
            if (existing is null)
            {
                repository.Add(new TelegramGroup(office, chatId, matter.Id, userId, conversation.Id, clock.UtcNow));
            }
            else
            {
                existing.Bind(matter.Id, userId, conversation.Id, clock.UtcNow);
            }

            return await repository.SaveChangesAsync(ct);
        }, ct);

        return $"Bu grup «{matter.Title}» dosyasına bağlandı. Gruptaki herkes Korkut'a bağlı ve dosyanın ekibindeyse "
            + "sorularınızı burada yanıtlarım. Gruba katılanları görebilmem için beni grupta yönetici yapın. "
            + "Müvekkil iletişim ve kimlik bilgilerini grupta paylaşmam.";
    }

    /// <summary>/ayir: undo the binding (same rule as making it).</summary>
    private async Task<string> UnbindAsync(Guid office, IReadOnlySet<string> permissions, TelegramGroup? group, CancellationToken ct)
    {
        if (group is null)
        {
            return "Bu grup bir dosyaya bağlı değil.";
        }

        if (!permissions.Contains(Permissions.MattersManageTeam) || !await SeesAsync(group.MatterId, ct))
        {
            return NotAllowed;
        }

        await unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var tracked = await repository.FindGroupAsync(group.ChatId, ct);
            if (tracked is not null)
            {
                repository.Remove(tracked);
            }

            return await repository.SaveChangesAsync(ct);
        }, ct);
        return "Grubun dosya bağı kaldırıldı.";
    }

    private async Task ContinueInNewAsync(Guid office, TelegramGroup group, CancellationToken ct)
    {
        var conversation = await sessions.CreateAsync(group.MatterId, ct);
        await unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var tracked = await repository.FindGroupAsync(group.ChatId, ct);
            tracked?.Continue(conversation.Id);
            return await repository.SaveChangesAsync(ct);
        }, ct);
        group.Continue(conversation.Id);
    }

    /// <summary>Whether the acting member can see the matter (their scope, as in the app).</summary>
    private async Task<bool> SeesAsync(Guid matterId, CancellationToken ct)
        => (await matters.ListAsync(ct)).Value?.Any(m => m.Id == matterId) == true;

    /// <summary>
    /// Whether every person in the group may see the matter: the bot must know each of them (the
    /// group's member count, less the bot, matches whom it has seen), and each must be a linked
    /// member on the matter's team or one who sees every matter.
    /// </summary>
    private async Task<bool> EveryoneMaySeeAsync(Guid office, string token, long chatId, long botId, Guid matterId, CancellationToken ct)
    {
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

    /// <summary>Remembers who is in the group from what a message shows: its sender, and who joined or left.</summary>
    private Task<int> TrackAsync(Guid office, long chatId, long botId, TelegramMessage message, CancellationToken ct)
        => unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var now = clock.UtcNow;
            if (message.From is { } from && from.Id != botId && message.LeftChatMember?.Id != from.Id)
            {
                await repository.AddGroupMemberAsync(office, chatId, from.Id, now, ct);
            }

            foreach (var joined in message.NewChatMembers ?? [])
            {
                if (joined.Id != botId)
                {
                    await repository.AddGroupMemberAsync(office, chatId, joined.Id, now, ct);
                }
            }

            if (message.LeftChatMember is { } left)
            {
                await repository.RemoveGroupMemberAsync(chatId, left.Id, ct);
            }

            return 0;
        }, ct);

    internal static IReadOnlySet<string> InGroup(IReadOnlySet<string> permissions)
        => permissions.Where(p => p is not (Permissions.ClientsContact or Permissions.ClientsIdentity)).ToHashSet();

    /// <summary>A bot's token starts with its user id ("123456:ABC…").</summary>
    internal static long BotId(string token)
        => long.TryParse(token.AsSpan(0, Math.Max(0, token.IndexOf(':', StringComparison.Ordinal))), out var id) ? id : 0;

    private async Task ReplyAsync(string token, long chatId, string text, CancellationToken ct)
    {
        foreach (var part in TelegramText.Split(text))
        {
            await telegram.SendMessageAsync(token, chatId, part, ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A Telegram group message could not be answered.")]
    private static partial void TurnFailed(ILogger logger, Exception ex);
}
