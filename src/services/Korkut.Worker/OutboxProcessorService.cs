using Korkut.Application.Jobs;
using Korkut.Core;

namespace Korkut.Worker;

/// <summary>Tuning for the outbox processor loop.</summary>
public sealed class OutboxProcessorOptions
{
    public string WorkerId { get; set; } = $"korkut-worker-{Environment.MachineName}";

    public int BatchSize { get; set; } = 32;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Idle delay between polls when a batch comes back empty.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Background loop that drains the transactional outbox using
/// <see cref="OutboxDispatcher"/>. Handlers, the outbox/inbox stores and the audit
/// sink come from DI: today the in-memory reference stores are registered; KDM-011
/// swaps in the EF-backed PostgreSQL store without changing this loop. A poison
/// message is dead-lettered by the dispatcher and never stalls the loop.
/// </summary>
public sealed partial class OutboxProcessorService(
    IOutboxStore outbox,
    IInboxStore inbox,
    IJobAudit audit,
    IEnumerable<IJobHandler> handlers,
    ITenantContext tenant,
    IRetryPolicy retry,
    IClock clock,
    OutboxProcessorOptions options,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var map = handlers.ToDictionary(h => h.MessageType, h => h);
        var dispatcher = new OutboxDispatcher(outbox, inbox, audit, map, tenant, retry, clock);
        LogStarted(logger, options.WorkerId, map.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            int handled;
            try
            {
                handled = await dispatcher.DispatchOnceAsync(
                    options.WorkerId, options.BatchSize, options.LeaseDuration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Back off only when idle; keep draining while there is work.
            if (handled == 0)
            {
                await Task.Delay(options.PollInterval, stoppingToken).ContinueWith(
                    _ => { }, TaskScheduler.Default);
            }
        }

        LogStopping(logger, options.WorkerId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox processor {WorkerId} started with {HandlerCount} handlers")]
    private static partial void LogStarted(ILogger logger, string workerId, int handlerCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox processor {WorkerId} stopping")]
    private static partial void LogStopping(ILogger logger, string workerId);
}
