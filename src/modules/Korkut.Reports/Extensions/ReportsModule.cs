using Korkut.Contract.Reports;
using Korkut.Core;
using Korkut.Reports.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Reports.Extensions;

/// <summary>
/// Self-registration for the Reports module: the dashboard orchestration service, which composes
/// the overview from each domain's Contract lookup. No cross-module query to register.
/// </summary>
public sealed class ReportsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IReportsService, ReportsService>();
    }
}
