using System.Text;
using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Contract.Telegram;
using Korkut.Core;
using Korkut.Telegram.Entities;
using Korkut.Telegram.Repositories;

namespace Korkut.Telegram.Services;

/// <summary>
/// The owner's side: connect a bot (the token is checked with Telegram, then sealed), turn it on
/// once they consent to messages passing through Telegram, see and unlink members.
/// </summary>
internal sealed class TelegramSettingsService(
    ITelegramRepository repository, ITelegramApi telegram, ISecretEncryptor encryptor, IUserLookup users,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : ITelegramSettingsService
{
    public Task<Result<TelegramSettingsResponse>> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office => Result<TelegramSettingsResponse>.Ok(await DescribeAsync(office, ct)), ct);

    public async Task<Result<TelegramSettingsResponse>> SaveAsync(SaveTelegramSettingsRequest request, CancellationToken ct = default)
    {
        var token = request.Token?.Trim();
        string? botUsername = null;
        if (!string.IsNullOrEmpty(token))
        {
            // Checked before any transaction opens: Telegram may be slow to answer.
            try
            {
                var me = await telegram.GetMeAsync(token, ct);
                botUsername = me.Username;
            }
            catch (TelegramApiException ex) when (ex.ErrorCode is 401 or 404)
            {
                return Result<TelegramSettingsResponse>.Unprocessable(
                    "Telegram bu bot anahtarını tanımadı. BotFather'ın verdiği anahtarı eksiksiz yapıştırın.", "telegram_token_rejected");
            }
            catch (TelegramApiException ex)
            {
                return Result<TelegramSettingsResponse>.Unprocessable(
                    $"Bot anahtarı denetlenemedi: {ex.Message}", "telegram_unreachable");
            }

            if (string.IsNullOrEmpty(botUsername))
            {
                return Result<TelegramSettingsResponse>.Unprocessable("Bu anahtar bir bota ait değil.", "telegram_token_rejected");
            }
        }

        return await InScopeAsync(async office =>
        {
            var bot = await repository.FindBotAsync(office, ct);

            // Validate before changing anything: the scope commits whatever ran before a refusal.
            if (request.Enabled && botUsername is null && bot?.TokenCipher is null)
            {
                return Result<TelegramSettingsResponse>.Unprocessable("Botu açmak için önce bot anahtarını girin.", "telegram_no_token");
            }

            if (request.Enabled && !request.Consent && bot?.ConsentAt is null)
            {
                return Result<TelegramSettingsResponse>.Unprocessable(
                    "Botu açmak için mesajların Telegram sunucularından geçeceğini onaylamanız gerekir.", "telegram_consent_required");
            }

            if (bot is null)
            {
                bot = new TelegramBot(office);
                repository.Add(bot);
            }

            if (botUsername is not null)
            {
                // A different bot means different chats: the old links point nowhere now.
                if (!string.Equals(bot.BotUsername, botUsername, StringComparison.OrdinalIgnoreCase))
                {
                    await repository.RemoveLinksAsync(office, ct);
                }

                bot.Connect(encryptor.Encrypt(Encoding.UTF8.GetBytes(token!)), botUsername, clock.UtcNow);
            }

            bot.Configure(request.Enabled, currentUser.UserId, request.Consent, clock.UtcNow);
            await repository.SaveChangesAsync(ct);
            return Result<TelegramSettingsResponse>.Ok(await DescribeAsync(office, ct));
        }, ct);
    }

    public Task<Result<TelegramSettingsResponse>> RemoveAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await repository.RemoveLinksAsync(office, ct);
            var bot = await repository.FindBotAsync(office, ct);
            if (bot is not null)
            {
                repository.Remove(bot);
                await repository.SaveChangesAsync(ct);
            }

            return Result<TelegramSettingsResponse>.Ok(await DescribeAsync(office, ct));
        }, ct);

    public Task<Result<TelegramSettingsResponse>> UnlinkMemberAsync(Guid userId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var link = await repository.FindLinkAsync(userId, ct);
            if (link is null || !link.IsLinked)
            {
                return Result<TelegramSettingsResponse>.NotFound("Bu üyenin bağlı bir Telegram hesabı yok.");
            }

            link.Unlink();
            await repository.SaveChangesAsync(ct);
            return Result<TelegramSettingsResponse>.Ok(await DescribeAsync(office, ct));
        }, ct);

    private async Task<TelegramSettingsResponse> DescribeAsync(Guid office, CancellationToken ct)
    {
        var bot = await repository.FindBotAsync(office, ct);
        var links = await repository.ListLinkedAsync(office, ct);
        var names = await users.DisplayNamesAsync(office, links.Select(l => l.UserId).ToList(), ct);
        var members = links
            .Select(l => new TelegramLinkedMember(l.UserId, names.GetValueOrDefault(l.UserId, "Üye"), l.TelegramUsername, l.LinkedAt!.Value))
            .ToList();
        return new TelegramSettingsResponse(
            bot?.TokenCipher is not null, bot?.Enabled ?? false, bot?.BotUsername, bot?.ConsentAt, members);
    }

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
