using Korkut.Calendar.Services;
using Korkut.Calendar.Repositories;
using Korkut.Contract.Calendar;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Calendar.Extensions;

/// <summary>
/// Self-registration for the Calendar module: binds the event repository + service. The unified
/// view (events ∪ deadlines ∪ hearings) is composed inside the service by calling each owner's
/// Contract lookup, so there is no cross-module read-model to register.
/// </summary>
public sealed class CalendarModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        services.AddScoped<ICalendarService, CalendarService>();
    }
}
