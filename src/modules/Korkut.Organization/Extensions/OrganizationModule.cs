using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract.Offices;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Korkut.Organization.Extensions;

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
        services.AddScoped<Korkut.Contract.Offices.IAdminConsoleService, AdminConsoleService>();

        // The dynamic model plane. The cache is process-wide (one snapshot per replica, TTL-refreshed)
        // so the chat router does not re-read admin config on every turn; the reader is scoped because
        // it needs the request's DbContext. OfficeModelResolver is the router's seam: office
        // assignment, then (interleaved with the local/hearing decision by the caller) the system
        // default — see IOfficeModelOverrideService for the full chain. The bootstrapper guarantees
        // that system default is never a silent gap on a fresh install; every host that wires this
        // module runs it once at startup.
        services.AddSingleton<ModelPlaneCache>();
        services.AddScoped<ModelPlaneReader>();
        services.AddScoped<Korkut.Contract.Agent.Model.IOfficeModelOverrideService, OfficeModelResolver>();
        services.AddScoped<Korkut.Contract.Offices.IModelCatalogService, ModelCatalogService>();
        services.AddScoped<Korkut.Contract.Offices.IUsageReportService, UsageReportService>();
        services.AddHostedService<PlatformDefaultBootstrapper>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<Korkut.Contract.Organization.IUserLookup, UserLookup>();
        services.AddScoped<Korkut.Core.IUserLookupService, UserLookupService>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<Korkut.Contract.Organization.IMemberService, MemberService>();
        services.AddScoped<Korkut.Contract.Organization.IProfileService, ProfileService>();
        services.AddScoped<Korkut.Contract.Organization.IInviteService, InviteService>();
        services.AddScoped<Korkut.Contract.Organization.IOnboardingService, OnboardingService>();
        services.AddScoped<Korkut.Contract.Organization.IAuthService, AuthService>();
        services.AddScoped<Korkut.Contract.Organization.IGoogleSignInService, GoogleSignInService>();
    }
}
