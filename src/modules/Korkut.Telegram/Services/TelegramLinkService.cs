using System.Security.Cryptography;
using System.Text;
using Korkut.Contract;
using Korkut.Contract.Telegram;
using Korkut.Core;
using Korkut.Telegram.Entities;
using Korkut.Telegram.Repositories;

namespace Korkut.Telegram.Services;

/// <summary>
/// A member's side: get a one-time code, send it to the bot from their own Telegram (the deep link
/// does that in one tap), and from then on that chat speaks to Korkut as them.
/// </summary>
internal sealed class TelegramLinkService(
    ITelegramRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ITelegramLinkService
{
    /// <summary>How long a code works; long enough to open Telegram, short enough not to linger.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    public Task<Result<TelegramLinkResponse>> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office => Result<TelegramLinkResponse>.Ok(await DescribeAsync(office, ct)), ct);

    public Task<Result<TelegramLinkCodeResponse>> CreateCodeAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var bot = await repository.FindBotAsync(office, ct);
            if (bot is not { Enabled: true, TokenCipher: not null, BotUsername: not null })
            {
                return Result<TelegramLinkCodeResponse>.Conflict("Büronun Telegram botu açık değil. Büro sahibinden açmasını isteyin.", "telegram_off");
            }

            var link = await repository.FindLinkAsync(currentUser.UserId, ct);
            if (link is null)
            {
                link = new TelegramLink(office, currentUser.UserId);
                repository.Add(link);
            }

            // Telegram passes /start parameters of up to 64 characters from [A-Za-z0-9_-].
            var code = Base64Url(RandomNumberGenerator.GetBytes(16));
            var expiresAt = clock.UtcNow.Add(CodeLifetime);
            link.IssueCode(HashCode(code), expiresAt);
            await repository.SaveChangesAsync(ct);
            return Result<TelegramLinkCodeResponse>.Ok(new TelegramLinkCodeResponse(code, $"https://t.me/{bot.BotUsername}?start={code}", expiresAt));
        }, ct);

    public Task<Result<TelegramLinkResponse>> UnlinkAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var link = await repository.FindLinkAsync(currentUser.UserId, ct);
            if (link is not null)
            {
                link.Unlink();
                await repository.SaveChangesAsync(ct);
            }

            return Result<TelegramLinkResponse>.Ok(await DescribeAsync(office, ct));
        }, ct);

    /// <summary>What is stored in place of a code: the code itself never is.</summary>
    public static string HashCode(string code)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private async Task<TelegramLinkResponse> DescribeAsync(Guid office, CancellationToken ct)
    {
        var bot = await repository.FindBotAsync(office, ct);
        var link = await repository.FindLinkAsync(currentUser.UserId, ct);
        var available = bot is { Enabled: true, TokenCipher: not null };
        return new TelegramLinkResponse(
            available, available ? bot!.BotUsername : null, link?.IsLinked ?? false, link?.TelegramUsername, link?.LinkedAt);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
