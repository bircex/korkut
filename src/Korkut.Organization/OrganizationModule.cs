using Korkut.Contract.Offices;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Organization;

/// <summary>
/// Self-registration for the Organization (identity/tenant) module. Grows in stages as users,
/// members, invites and the auth-token aggregates move in; the auth security plumbing stays in
/// Infrastructure/Web and consumes this module's repositories.
/// </summary>
public sealed class OrganizationModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IOfficeAdminService, OfficeAdminService>();
    }
}
