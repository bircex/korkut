using Korkut.Contract.Agent.Chat;
using Korkut.Contract.Offices;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Security;
using Korkut.Telegram.Repositories;
using Microsoft.Extensions.Logging;

namespace Korkut.Telegram.Services;

/// <summary>
/// Answers one message sent to the office bot, in its own DI scope. A code links the chat to the
/// member who asked for it; after that, every message runs a Korkut chat turn as that member, with
/// exactly the permissions their role gives them in the app, and the reply goes back to the chat.
/// </summary>
public sealed partial class TelegramUpdateHandler(
    ITelegramRepository repository, ITelegramApi telegram, IUnitOfWork unitOfWork, IUserLookup users,
    AuthorizationService authorization, ActingUser acting, ISessionAppService sessions, ISessionChatService chat,
    IClock clock, ILogger<TelegramUpdateHandler> logger)
{
    private const string NotLinked =
        "Merhaba! Bu bot bir Korkut bürosuna bağlı. Kullanmak için Korkut'ta Ayarlar → Hesap bölümünden \"Telegram'ı bağla\" düğmesine basın.";

    private const string Help =
        "Sorunuzu ya da isteğinizi yazmanız yeterli; Korkut'taki sohbetle aynı şekilde yanıt veririm.\n\n/yeni ile yeni bir sohbet başlatabilirsiniz.";

    private const string Failed = "Bir sorun oluştu ve yanıt veremedim. Biraz sonra yeniden deneyin.";

    public async Task HandleAsync(InstallOffice office, string token, TelegramUpdate update, CancellationToken ct = default)
    {
        var message = update.Message;
        if (message is null || message.Chat.Type != "private")
        {
            return; // groups and channels are not a member's own chat
        }

        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            await ReplyAsync(token, chatId, "Şimdilik yalnızca yazılı mesajları yanıtlayabiliyorum.", ct);
            return;
        }

        var (command, argument) = ParseCommand(text);
        if (command == "/start" && argument.Length > 0)
        {
            await LinkAsync(office.Id, token, chatId, message.From?.Username, argument, ct);
            return;
        }

        var link = await unitOfWork.InTenantScopeAsync(office.Id, () => repository.FindLinkByChatAsync(chatId, ct), ct);
        var role = link is null ? null : await unitOfWork.InTenantScopeAsync(office.Id, () => users.RoleAsync(office.Id, link.UserId, ct), ct);
        if (link is null || role is null)
        {
            await ReplyAsync(token, chatId, NotLinked, ct);
            return;
        }

        switch (command)
        {
            case "/start":
            case "/yardim":
            case "/help":
                await ReplyAsync(token, chatId, Help, ct);
                return;
            case "/yeni":
            case "/new":
                await ContinueAsync(office.Id, link.UserId, null, ct);
                await ReplyAsync(token, chatId, "Yeni bir sohbet başladı. Ne yapmamı istersiniz?", ct);
                return;
        }

        // From here on the scope speaks as the member, so every tool sees them as the caller.
        var permissions = authorization.ResolvePermissions(role);
        acting.Set(office.Id, link.UserId, permissions);

        using var typing = KeepTyping(token, chatId, ct);
        try
        {
            var conversation = link.ConversationId ?? await StartConversationAsync(office.Id, link.UserId, ct);
            var outcome = await chat.ChatAsync(office.Id, link.UserId, conversation, text, permissions, ct);
            if (outcome is null)
            {
                // The conversation was deleted in the app: carry on in a new one.
                conversation = await StartConversationAsync(office.Id, link.UserId, ct);
                outcome = await chat.ChatAsync(office.Id, link.UserId, conversation, text, permissions, ct);
            }

            await typing.StopAsync();
            await ReplyAsync(token, chatId, string.IsNullOrWhiteSpace(outcome?.Reply) ? Failed : outcome.Reply, ct);
        }
        catch (Exception ex) when (ex is ModelUnavailableException or BudgetExceededException)
        {
            await typing.StopAsync();
            await ReplyAsync(token, chatId, ex.Message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TurnFailed(logger, ex);
            await typing.StopAsync();
            await ReplyAsync(token, chatId, Failed, ct);
        }
    }

    private async Task LinkAsync(Guid office, string token, long chatId, string? username, string code, CancellationToken ct)
    {
        var linked = await unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var link = await repository.FindLinkByCodeAsync(TelegramLinkService.HashCode(code), ct);
            if (link is null || link.CodeExpiresAt is null || link.CodeExpiresAt < clock.UtcNow)
            {
                return false;
            }

            // A chat speaks for one member: whoever it spoke for before is unlinked first.
            var previous = await repository.FindLinkByChatAsync(chatId, ct);
            if (previous is not null && previous.UserId != link.UserId)
            {
                previous.Unlink();
                await repository.SaveChangesAsync(ct);
            }

            link.Link(chatId, username, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return true;
        }, ct);

        await ReplyAsync(token, chatId, linked
            ? "Hesabınız bağlandı. Artık buradan Korkut'a yazabilirsiniz.\n\n" + Help
            : "Bu kod geçersiz ya da süresi dolmuş. Korkut'ta Ayarlar → Hesap bölümünden yeni bir kod alın.", ct);
    }

    private async Task<Guid> StartConversationAsync(Guid office, Guid userId, CancellationToken ct)
    {
        var session = await sessions.CreateAsync(null, ct);
        await ContinueAsync(office, userId, session.Id, ct);
        return session.Id;
    }

    private Task<int> ContinueAsync(Guid office, Guid userId, Guid? conversationId, CancellationToken ct)
        => unitOfWork.InTenantScopeAsync(office, async () =>
        {
            var link = await repository.FindLinkAsync(userId, ct);
            link?.Continue(conversationId);
            return await repository.SaveChangesAsync(ct);
        }, ct);

    private async Task ReplyAsync(string token, long chatId, string text, CancellationToken ct)
    {
        foreach (var part in TelegramText.Split(text))
        {
            await telegram.SendMessageAsync(token, chatId, part, ct);
        }
    }

    /// <summary>"/start abc" → ("/start", "abc"); "/yeni@KorkutBot" → ("/yeni", ""); plain text → ("", "").</summary>
    internal static (string Command, string Argument) ParseCommand(string text)
    {
        if (!text.StartsWith('/'))
        {
            return (string.Empty, string.Empty);
        }

        var space = text.IndexOfAny([' ', '\n']);
        var head = space < 0 ? text : text[..space];
        var at = head.IndexOf('@', StringComparison.Ordinal);
        var command = (at < 0 ? head : head[..at]).ToLowerInvariant();
        return (command, space < 0 ? string.Empty : text[(space + 1)..].Trim());
    }

    /// <summary>Telegram shows "typing…" for about five seconds per call, so it is renewed until the reply is ready.</summary>
    private Typing KeepTyping(string token, long chatId, CancellationToken ct) => new(telegram, token, chatId, ct);

    private sealed class Typing : IDisposable
    {
        private readonly CancellationTokenSource _stop;
        private readonly Task _loop;

        public Typing(ITelegramApi telegram, string token, long chatId, CancellationToken ct)
        {
            _stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var stop = _stop.Token;
            _loop = Task.Run(async () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        await telegram.SendTypingAsync(token, chatId, stop);
                        await Task.Delay(TimeSpan.FromSeconds(4), stop);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (TelegramApiException)
                    {
                        return; // only a courtesy; the reply still goes out
                    }
                }
            }, CancellationToken.None);
        }

        public async Task StopAsync()
        {
            await _stop.CancelAsync();
            await _loop;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _stop.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A Telegram message could not be answered.")]
    private static partial void TurnFailed(ILogger logger, Exception ex);
}
