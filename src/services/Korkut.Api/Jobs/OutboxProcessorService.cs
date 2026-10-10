using Korkut.Core;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;

namespace Korkut.Api.Jobs;

/// <summary>
/// Runs queued background jobs (the transactional outbox) inside the app host, where every
/// handler's services already live. Each pass leases a batch in its own DI scope, because the
/// stores and handlers work through the per-scope DbContext. Leasing is SKIP LOCKED, so a second
/// replica would share the queue rather than double-run it. Idle passes back off; busy ones drain.
/// </summary>
public sealed partial class OutboxProcessorService(
    IServiceScopeFactory scopes, IClock clock, ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private const int BatchSize = 16;

    private readonly string _owner = $"korkut-{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int handled;
            try
            {
                handled = await DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The database being briefly unreachable must not end the loop for good.
                PassFailed(logger, ex);
                handled = 0;
            }

            if (handled == 0)
            {
                await Task.Delay(PollInterval, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
            }
        }
    }

    private async Task<int> DispatchOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dispatcher = new OutboxDispatcher(
            services.GetRequiredService<IOutboxStore>(),
            services.GetRequiredService<IInboxStore>(),
            services.GetRequiredService<IJobAudit>(),
            services.GetServices<IJobHandler>().ToDictionary(h => h.MessageType),
            services.GetRequiredService<ITenantContext>(),
            services.GetRequiredService<IRetryPolicy>(),
            clock);
        return await dispatcher.DispatchOnceAsync(_owner, BatchSize, LeaseDuration, ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Background job pass failed; retrying shortly.")]
    private static partial void PassFailed(ILogger logger, Exception ex);
}
