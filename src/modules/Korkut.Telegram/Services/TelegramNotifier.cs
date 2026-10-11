using System.Text;
using Korkut.Contract.Telegram;
using Korkut.Core;
using Korkut.Telegram.Repositories;
using Microsoft.Extensions.Logging;

namespace Korkut.Telegram.Services;

/// <summary>
/// <see cref="ITelegramNotifier"/> over the office bot. Reads the bot and links in one short tenant
/// scope, then sends outside it; one member's unreachable chat does not stop the others.
/// </summary>
internal sealed partial class TelegramNotifier(
    ITelegramRepository repository, ITelegramApi telegram, ISecretEncryptor encryptor, IUnitOfWork unitOfWork,
    ILogger<TelegramNotifier> logger) : ITelegramNotifier
{
    public async Task<int> NotifyAsync(Guid officeId, IReadOnlyCollection<Guid>? userIds, string text, CancellationToken ct = default)
    {
        var (token, chats) = await unitOfWork.InTenantScopeAsync(officeId, async () =>
        {
            var bot = await repository.FindBotAsync(officeId, ct);
            if (bot is not { Enabled: true, TokenCipher: not null })
            {
                return (null, Array.Empty<long>());
            }

            var linked = await repository.ListLinkedAsync(officeId, ct);
            var chatIds = linked
                .Where(l => userIds is null || userIds.Contains(l.UserId))
                .Select(l => l.ChatId!.Value)
                .ToArray();
            return ((string?)Encoding.UTF8.GetString(encryptor.Decrypt(bot.TokenCipher)), chatIds);
        }, ct);

        if (token is null)
        {
            return 0;
        }

        var reached = 0;
        foreach (var chatId in chats)
        {
            try
            {
                foreach (var part in TelegramText.Split(text))
                {
                    await telegram.SendMessageAsync(token, chatId, part, ct);
                }

                reached++;
            }
            catch (TelegramApiException ex)
            {
                NotDelivered(logger, ex.ErrorCode, ex.Message);
            }
        }

        return reached;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A Telegram notice was not delivered ({Code}): {Reason}")]
    private static partial void NotDelivered(ILogger logger, int? code, string reason);
}
