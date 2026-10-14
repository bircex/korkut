using Korkut.Contract.Offices;
using Korkut.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Korkut.Reminders.Services;

/// <summary>Runs a reminder pass for the install's office once a minute, each in its own scope.</summary>
public sealed partial class ReminderSchedulerService(
    IServiceScopeFactory scopes, IClock clock, ILogger<ReminderSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var office = await scope.ServiceProvider.GetRequiredService<IOfficeProvisioner>().InstallOfficeAsync(stoppingToken);
                if (office is { Disabled: false })
                {
                    await scope.ServiceProvider.GetRequiredService<ReminderService>().RunOnceAsync(office.Id, clock.UtcNow, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                PassFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).AsTask().ContinueWith(t => !t.IsCanceled && t.Result, TaskScheduler.Default));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reminder pass failed; retrying in a minute.")]
    private static partial void PassFailed(ILogger logger, Exception ex);
}
