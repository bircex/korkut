namespace Korkut.Worker;

/// <summary>
/// Background host skeleton. The real work — transactional-outbox consumer, file
/// scanning, OCR, extraction, indexing projections and notifications — is added in
/// KDM-008+ as idempotent, tenant-scoped handlers. For now it just proves the
/// worker host boots and shuts down cleanly.
/// </summary>
public sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        stoppingToken.Register(() => LogStopping(logger));
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Korkut.Worker started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Korkut.Worker stopping")]
    private static partial void LogStopping(ILogger logger);
}
