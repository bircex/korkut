using Korkut.Contract.Agent.Chat;
using Korkut.Contract.Matters;
using Korkut.Contract.Offices;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Telegram.Entities;
using Korkut.Telegram.Repositories;
using Microsoft.Extensions.Logging;

namespace Korkut.Telegram.Services;

/// <summary>
/// Answers one message sent to the office bot, in its own DI scope. A code links the chat to the
/// member who asked for it; after that, every message runs a Korkut chat turn as that member, with
/// exactly the permissions their role gives them in the app, and the reply goes back to the chat.
/// A conversation belongs to a matter or is personal, as in the app: /dosya moves to one of the
/// member's matters, /kisisel back to a personal conversation, and a matter the member is not on is
/// answered without saying whether it exists.
/// </summary>
public sealed partial class TelegramUpdateHandler(
    ITelegramRepository repository, ITelegramApi telegram, IUnitOfWork unitOfWork, IUserLookup users,
    IRolePermissions rolePermissions, ActingUser acting, ISessionAppService sessions, ISessionChatService chat,
    IMatterService matters, IClock clock, ILogger<TelegramUpdateHandler> logger)
{
    private const string NotLinked =
        "Merhaba! Bu bot bir Korkut bürosuna bağlı. Kullanmak için Korkut'ta Ayarlar → Hesap bölümünden \"Telegram'ı bağla\" düğmesine basın.";

    private const string Help =
        "Sorunuzu ya da isteğinizi yazmanız yeterli; Korkut'taki sohbetle aynı şekilde yanıt veririm. "
        + "Belge ya da fotoğraf da gönderebilirsiniz; ne istediğinizi açıklamasına yazın.\n\n"
        + "/dosyalar ile dosyalarınızı görürsünüz, /dosya <ad ya da esas no> ile birine geçersiniz. "
        + "/kisisel kişisel sohbete döner, /yeni bulunduğunuz yerde yeni bir sohbet başlatır.";

    private const string NoPersonal =
        "Kişisel sohbet açma yetkiniz yok. /dosyalar ile dosyalarınızı görüp /dosya <ad ya da esas no> ile birine geçebilirsiniz.";

    private const string NoSuchMatter = "Bu dosya bulunamadı veya yetkiniz yok.";

    private const string Failed = "Bir sorun oluştu ve yanıt veremedim. Biraz sonra yeniden deneyin.";

    public async Task HandleAsync(InstallOffice office, string token, TelegramUpdate update, CancellationToken ct = default)
    {
        var message = update.Message;
        if (message is null || message.Chat.Type != "private")
        {
            return; // groups and channels are not a member's own chat
        }

        var chatId = message.Chat.Id;
        var attachment = Attachment(message);
        var text = (message.Text ?? message.Caption)?.Trim() ?? string.Empty;
        if (attachment is null && text.Length == 0)
        {
            await ReplyAsync(token, chatId, "Yazılı mesajları, belgeleri ve fotoğrafları yanıtlayabiliyorum.", ct);
            return;
        }

        var (command, argument) = attachment is null ? ParseCommand(text) : (string.Empty, string.Empty);
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

        // From here on the scope speaks as the member, so every service and tool sees them as the caller.
        var permissions = await rolePermissions.ForRoleAsync(office.Id, role, ct);
        acting.Set(office.Id, link.UserId, role, permissions);

        switch (command)
        {
            case "/start":
            case "/yardim":
            case "/help":
                await ReplyAsync(token, chatId, Help, ct);
                return;
            case "/yeni":
            case "/new":
                await ReplyAsync(token, chatId, await NewConversationAsync(office.Id, link, permissions, ct), ct);
                return;
            case "/dosyalar" or "/dosya" when !permissions.Contains(Permissions.MattersRead):
                await ReplyAsync(token, chatId, "Dosyaları görme yetkiniz yok.", ct);
                return;
            case "/dosyalar":
                await ReplyAsync(token, chatId, await MatterListAsync(office.Id, link, ct), ct);
                return;
            case "/dosya":
                await ReplyAsync(token, chatId, await SwitchToMatterAsync(office.Id, link.UserId, argument, ct), ct);
                return;
            case "/kisisel":
                if (!permissions.Contains(Permissions.ChatGeneral))
                {
                    await ReplyAsync(token, chatId, NoPersonal, ct);
                    return;
                }

                await ContinueAsync(office.Id, link.UserId, null, ct);
                await ReplyAsync(token, chatId, "Kişisel sohbete geçtim; yeni bir sohbet başladı. Ne yapmamı istersiniz?", ct);
                return;
        }

        if (attachment is { Size: > TelegramApi.MaxDownloadBytes })
        {
            await ReplyAsync(token, chatId, "Bu dosya Telegram üzerinden alınamayacak kadar büyük (en fazla 20 MB). Korkut'a tarayıcıdan yükleyebilirsiniz.", ct);
            return;
        }

        var reply = new StreamingReply(telegram, token, chatId);
        using var typing = KeepTyping(token, chatId, ct);
        try
        {
            var answer = attachment is null
                ? await ChatAsync(office.Id, link, text, permissions, reply, typing, ct)
                : await UploadAsync(office.Id, link, attachment, token, text, permissions, ct);
            await typing.StopAsync();
            await reply.FinishAsync(string.IsNullOrWhiteSpace(answer) ? Failed : answer, ct);
        }
        catch (Exception ex) when (ex is ModelUnavailableException or BudgetExceededException or TelegramApiException or ForbiddenException)
        {
            await typing.StopAsync();
            await reply.FinishAsync(Interrupted(reply, ex.Message), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TurnFailed(logger, ex);
            await typing.StopAsync();
            await reply.FinishAsync(Interrupted(reply, Failed), ct);
        }
    }

    /// <summary>A text turn, shown as it is written.</summary>
    private async Task<string?> ChatAsync(
        Guid office, TelegramLink link, string text, IReadOnlySet<string> permissions, StreamingReply reply, Typing typing, CancellationToken ct)
    {
        async Task OnToken(string delta)
        {
            await typing.StopAsync(); // the message itself now shows progress
            await reply.AppendAsync(delta, ct);
        }

        var conversation = link.ConversationId ?? await StartConversationAsync(office, link.UserId, ct);
        var outcome = await chat.ChatStreamAsync(office, link.UserId, conversation, text, permissions, _ => Task.CompletedTask, OnToken, ct: ct);
        if (outcome is null)
        {
            // The conversation was deleted in the app: carry on in a new one.
            conversation = await StartConversationAsync(office, link.UserId, ct);
            outcome = await chat.ChatStreamAsync(office, link.UserId, conversation, text, permissions, _ => Task.CompletedTask, OnToken, ct: ct);
        }

        return outcome?.Reply;
    }

    /// <summary>A document or photo: fetched from Telegram and taken in like an upload in the app
    /// (text extracted, scans read by OCR), with the caption as the question.</summary>
    private async Task<string?> UploadAsync(
        Guid office, TelegramLink link, TelegramAttachment attachment, string token, string caption,
        IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var bytes = await telegram.DownloadFileAsync(token, attachment.FileId, ct);
        var conversation = link.ConversationId ?? await StartConversationAsync(office, link.UserId, ct);
        var outcome = await UploadToAsync(conversation);
        if (outcome is null)
        {
            conversation = await StartConversationAsync(office, link.UserId, ct);
            outcome = await UploadToAsync(conversation);
        }

        return outcome?.Reply;

        async Task<UploadOutcome?> UploadToAsync(Guid id)
        {
            using var content = new MemoryStream(bytes, writable: false);
            return await chat.UploadAsync(office, link.UserId, id, attachment.FileName, attachment.ContentType, content, caption, permissions, ct);
        }
    }

    /// <summary>What a member sent besides text, if anything: a document, or the largest size of a photo.</summary>
    internal static TelegramAttachment? Attachment(TelegramMessage message)
    {
        if (message.Document is { } document)
        {
            return new TelegramAttachment(
                document.FileId, string.IsNullOrWhiteSpace(document.FileName) ? "belge" : document.FileName,
                document.MimeType ?? "application/octet-stream", document.FileSize);
        }

        return message.Photo is { Count: > 0 } sizes
            ? sizes.MaxBy(p => (long)p.Width * p.Height) is { } photo
                ? new TelegramAttachment(photo.FileId, $"foto-{message.MessageId}.jpg", "image/jpeg", photo.FileSize)
                : null
            : null;
    }

    /// <summary>When a reply breaks off, what was already written stays, followed by why.</summary>
    private static string Interrupted(StreamingReply reply, string why)
        => reply.Text.Trim().Length == 0 ? why : $"{reply.Text.Trim()}\n\n({why})";

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

    /// <summary>/yeni: a fresh conversation where the member is, in the same matter or a personal one.</summary>
    private async Task<string> NewConversationAsync(Guid office, TelegramLink link, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        if (await CurrentMatterAsync(link, ct) is { } matter)
        {
            var session = await sessions.CreateAsync(matter.Id, ct);
            await ContinueAsync(office, link.UserId, session.Id, ct);
            return $"«{matter.Title}» dosyasında yeni bir sohbet başladı. Ne yapmamı istersiniz?";
        }

        if (!permissions.Contains(Permissions.ChatGeneral))
        {
            return NoPersonal;
        }

        await ContinueAsync(office, link.UserId, null, ct);
        return "Yeni bir sohbet başladı. Ne yapmamı istersiniz?";
    }

    /// <summary>/dosyalar: the matters the member can see, the current one marked.</summary>
    private async Task<string> MatterListAsync(Guid office, TelegramLink link, CancellationToken ct)
    {
        var visible = (await matters.ListAsync(ct)).Value ?? [];
        if (visible.Count == 0)
        {
            return "Şu an erişebildiğiniz bir dosya yok. Bir dosyanın ekibine eklendiğinizde burada görünür.";
        }

        var current = (await CurrentMatterAsync(link, ct))?.Id;
        var lines = visible
            .OrderBy(m => m.Status == "open" ? 0 : 1).ThenBy(m => m.Title, StringComparer.InvariantCulture)
            .Take(MaxListed)
            .Select(m => $"{(m.Id == current ? "▸" : "•")} {Describe(m)}");
        var more = visible.Count > MaxListed ? $"\n… ve {visible.Count - MaxListed} dosya daha." : string.Empty;
        return $"Dosyalarınız:\n{string.Join('\n', lines)}{more}\n\nBirine geçmek için /dosya <ad ya da esas no> yazın.";
    }

    /// <summary>/dosya: move to one of the member's matters, in a new conversation there.</summary>
    private async Task<string> SwitchToMatterAsync(Guid office, Guid userId, string query, CancellationToken ct)
    {
        if (query.Length == 0)
        {
            return "Hangi dosya? Örnek: /dosya Kira davası ya da /dosya 2026/123. Dosyalarınızı /dosyalar ile görebilirsiniz.";
        }

        var found = Find((await matters.ListAsync(ct)).Value ?? [], query);
        if (found.Count == 0)
        {
            return NoSuchMatter;
        }

        if (found.Count > 1)
        {
            var options = string.Join('\n', found.Take(MaxListed).Select(m => $"• {Describe(m)}"));
            return $"Birden çok dosya eşleşti:\n{options}\n\nTam adıyla ya da esas numarasıyla yazın.";
        }

        var matter = found[0];
        var session = await sessions.CreateAsync(matter.Id, ct);
        await ContinueAsync(office, userId, session.Id, ct);
        return $"«{matter.Title}» dosyasına geçtim; bu dosyada yeni bir sohbet başladı. "
            + "Yanıtlarım ve işlemlerim artık bu dosyayla sınırlı.";
    }

    private const int MaxListed = 30;

    private static string Describe(MatterResponse m)
        => m.FileNo is { Length: > 0 } no ? $"{m.Title} ({no})" : m.Title;

    /// <summary>
    /// The matters a query names: an exact file number or title wins, otherwise every title that
    /// contains it. Compared without case or Turkish letters, so "kira" finds "KİRA".
    /// </summary>
    internal static IReadOnlyList<MatterResponse> Find(IReadOnlyList<MatterResponse> matters, string query)
    {
        var q = Fold(query);
        var exact = matters.Where(m => Fold(m.FileNo ?? string.Empty) == q || Fold(m.Title) == q).ToList();
        return exact.Count > 0 ? exact : matters.Where(m => Fold(m.Title).Contains(q, StringComparison.Ordinal)).ToList();
    }

    // The build runs with invariant globalization, which leaves Turkish capitals such as İ as they
    // are, so the Turkish letters are folded by hand.
    private static string Fold(string text)
        => new(text.Trim().Select(c => c switch
        {
            'ç' or 'Ç' => 'c',
            'ğ' or 'Ğ' => 'g',
            'ı' or 'I' or 'İ' => 'i',
            'ö' or 'Ö' => 'o',
            'ş' or 'Ş' => 's',
            'ü' or 'Ü' => 'u',
            _ => char.ToLowerInvariant(c),
        }).Where(c => c != '\u0307').ToArray());

    /// <summary>The matter of the conversation the member is in, if any and still theirs to see.</summary>
    private async Task<MatterResponse?> CurrentMatterAsync(TelegramLink link, CancellationToken ct)
    {
        if (link.ConversationId is not { } id)
        {
            return null;
        }

        try
        {
            var conversation = await sessions.GetAsync(id, ct);
            return conversation.MatterId is { } matterId
                ? (await matters.ListAsync(ct)).Value?.FirstOrDefault(m => m.Id == matterId)
                : null;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private async Task<Guid> StartConversationAsync(Guid office, Guid userId, CancellationToken ct)
    {
        // A conversation outside any matter is a personal one, which takes its own permission.
        if (!acting.Current!.Permissions.Contains(Permissions.ChatGeneral))
        {
            throw new ForbiddenException(NoPersonal);
        }

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

/// <summary>A file a member sent: what to fetch, and how to name and read it.</summary>
internal sealed record TelegramAttachment(string FileId, string FileName, string ContentType, long? Size);
