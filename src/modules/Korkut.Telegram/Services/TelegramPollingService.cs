using System.Collections.Concurrent;
using System.Text;
using Korkut.Contract.Offices;
using Korkut.Core;
using Korkut.Telegram.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Korkut.Telegram.Services;

/// <summary>
/// Listens to the office bot by long polling, so a self-hosted install needs no public address.
/// The settings are read again on every pass: turning the bot on, off or onto another token takes
/// effect within one poll. Each message is answered in its own scope; one chat's messages are
/// answered in order, different chats side by side. The place in Telegram's queue is saved before
/// answering, so a restart never answers the same message twice.
/// </summary>
public sealed partial class TelegramPollingService(
    IServiceScopeFactory scopes, ITelegramApi telegram, ILogger<TelegramPollingService> logger) : BackgroundService
{
    private const int PollSeconds = 25;
    private const int MaxParallelChats = 4;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<long, SemaphoreSlim> _chats = new();
    private readonly SemaphoreSlim _parallel = new(MaxParallelChats);
    private readonly ConcurrentDictionary<Task, byte> _running = new();
    private int? _lastError;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan wait;
            try
            {
                wait = await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (TelegramApiException ex)
            {
                wait = OnTelegramError(ex);
            }
            catch (Exception ex)
            {
                PassFailed(logger, ex);
                wait = IdleDelay;
            }

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }

        await Task.WhenAll(_running.Keys);
    }

    /// <summary>One poll; returns how long to wait before the next.</summary>
    private async Task<TimeSpan> PollOnceAsync(CancellationToken ct)
    {
        var bot = await LoadAsync(ct);
        if (bot is null)
        {
            _lastError = null;
            return IdleDelay;
        }

        var (office, token, offset) = bot.Value;
        var updates = await telegram.GetUpdatesAsync(token, offset, PollSeconds, ct);
        if (_lastError is not null)
        {
            Recovered(logger);
            _lastError = null;
        }

        if (updates.Count == 0)
        {
            return TimeSpan.Zero;
        }

        await SaveOffsetAsync(office.Id, updates.Max(u => u.UpdateId) + 1, ct);
        foreach (var update in updates)
        {
            Dispatch(office, token, update, ct);
        }

        return TimeSpan.Zero;
    }

    private async Task<(InstallOffice Office, string Token, long Offset)?> LoadAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var office = await services.GetRequiredService<IOfficeProvisioner>().InstallOfficeAsync(ct);
        if (office is null || office.Disabled)
        {
            return null;
        }

        var bot = await services.GetRequiredService<IUnitOfWork>().InTenantScopeAsync(
            office.Id, () => services.GetRequiredService<ITelegramRepository>().FindBotAsync(office.Id, ct), ct);
        if (bot is not { Enabled: true, TokenCipher: not null })
        {
            return null;
        }

        var token = Encoding.UTF8.GetString(services.GetRequiredService<ISecretEncryptor>().Decrypt(bot.TokenCipher));
        return (office, token, bot.UpdateOffset);
    }

    private async Task SaveOffsetAsync(Guid office, long next, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var repository = services.GetRequiredService<ITelegramRepository>();
        await services.GetRequiredService<IUnitOfWork>().InTenantScopeAsync(office, async () =>
        {
            var bot = await repository.FindBotAsync(office, ct);
            bot?.Advance(next);
            return await repository.SaveChangesAsync(ct);
        }, ct);
    }

    private void Dispatch(InstallOffice office, string token, TelegramUpdate update, CancellationToken ct)
    {
        var chatId = update.Message?.Chat.Id ?? 0;
        var turn = _chats.GetOrAdd(chatId, _ => new SemaphoreSlim(1));
        var task = Task.Run(async () =>
        {
            await turn.WaitAsync(ct);
            try
            {
                await _parallel.WaitAsync(ct);
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<TelegramUpdateHandler>().HandleAsync(office, token, update, ct);
                }
                finally
                {
                    _parallel.Release();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                AnswerFailed(logger, ex);
            }
            finally
            {
                turn.Release();
            }
        }, CancellationToken.None);
        _running.TryAdd(task, 0);
        _ = task.ContinueWith(t => _running.TryRemove(t, out _), TaskScheduler.Default);
    }

    private TimeSpan OnTelegramError(TelegramApiException ex)
    {
        // Say it once per kind of failure, not on every retry.
        var first = _lastError != ex.ErrorCode;
        _lastError = ex.ErrorCode;
        switch (ex.ErrorCode)
        {
            case 401 or 404:
                if (first)
                {
                    TokenRejected(logger);
                }

                return TimeSpan.FromMinutes(1);
            case 409:
                if (first)
                {
                    BotInUse(logger);
                }

                return TimeSpan.FromSeconds(30);
            case 429:
                return TimeSpan.FromSeconds(Math.Clamp(ex.RetryAfter ?? 5, 1, 300));
            default:
                if (first)
                {
                    Unreachable(logger, ex.Message);
                }

                return TimeSpan.FromSeconds(15);
        }
    }

    public override void Dispose()
    {
        _parallel.Dispose();
        base.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram rejected the bot token; check Ayarlar → Telegram.")]
    private static partial void TokenRejected(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Another program (or a webhook) is reading this bot's messages; only one Korkut can use a bot.")]
    private static partial void BotInUse(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram could not be reached: {Reason}")]
    private static partial void Unreachable(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Telegram bot is receiving messages again.")]
    private static partial void Recovered(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telegram poll failed; retrying shortly.")]
    private static partial void PassFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "A Telegram message could not be answered.")]
    private static partial void AnswerFailed(ILogger logger, Exception ex);
}
