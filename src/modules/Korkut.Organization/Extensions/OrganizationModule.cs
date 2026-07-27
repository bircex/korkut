using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract.Offices;
using Korkut.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<Korkut.Contract.Organization.IMemberService, MemberService>();
        services.AddScoped<Korkut.Contract.Organization.IProfileService, ProfileService>();
        services.AddScoped<Korkut.Contract.Organization.IInviteService, InviteService>();
        services.AddScoped<Korkut.Contract.Organization.IOnboardingService, OnboardingService>();
        services.AddScoped<Korkut.Contract.Organization.IAuthService, AuthService>();
        services.AddScoped<Korkut.Contract.Organization.IGoogleSignInService, GoogleSignInService>();
    }
}
