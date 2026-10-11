using Korkut.Core;
using Korkut.Reminders.Repositories;
using Korkut.Reminders.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Reminders.Extensions;

/// <summary>
/// Self-registration for the Reminders module. The scheduler itself is a hosted service the app
/// host adds with its other background work (<see cref="ReminderSchedulerService"/>).
/// </summary>
public sealed class RemindersModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISentReminderRepository, SentReminderRepository>();
        services.AddScoped<ReminderService>();
    }
}
