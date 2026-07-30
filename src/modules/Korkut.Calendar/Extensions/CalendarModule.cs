using Korkut.Calendar.Services;
using Korkut.Contract.Calendar;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Calendar.Extensions;

/// <summary>
/// Self-registration for the Calendar module: binds the read service only. The calendar owns no
/// table — the unified view (agenda records ∪ deadlines ∪ hearings) is composed inside the service
/// by calling each owner's Contract lookup, so there is nothing else to register.
/// </summary>
public sealed class CalendarModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICalendarService, CalendarService>();
    }
}
