using Korkut.Contract.Calendar;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Calendar;

/// <summary>
/// Self-registration for the Calendar module: binds the event repository + service. The
/// cross-module read-model (<see cref="ICalendarQuery"/>) is registered by the host, since its
/// impl spans other modules' tables.
/// </summary>
public sealed class CalendarModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        services.AddScoped<ICalendarService, CalendarService>();
    }
}
