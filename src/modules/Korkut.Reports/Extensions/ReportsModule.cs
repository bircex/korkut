using Korkut.Contract.Reports;
using Korkut.Core;
using Korkut.Reports.Repositories;
using Korkut.Reports.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Reports.Extensions;

/// <summary>
/// Self-registration for the Reports module: the dashboard orchestration service. The
/// cross-module aggregation query (<see cref="IReportsQuery"/>) reads other modules' tables,
/// so its implementation stays in Infrastructure and is registered by the host.
/// </summary>
public sealed class ReportsModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IReportsService, ReportsService>();
    }
}
