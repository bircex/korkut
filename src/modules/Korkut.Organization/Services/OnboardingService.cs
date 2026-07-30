using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization.Services;

/// <summary>
/// Onboarding over the provisioner port. For the Google path the identity is not taken from the
/// request body: the caller passes the e-mail and subject Google verified (a signed, short-lived
/// ticket). For local registration the body carries the e-mail + password (hashed here). Either way
/// the office is created disabled and waits for a platform admin.
/// </summary>
public sealed class OnboardingService(IOfficeProvisioner provisioner, IPasswordHasher passwordHasher) : IOnboardingService
{
    public async Task<Result<DemoRequestResponse>> RequestOfficeAsync(
        string email, string googleSub, DemoRequestRequest request, CancellationToken ct = default)
    {
        var provisioned = await provisioner.ProvisionAsync(email, googleSub, request.OfficeName!.Trim(), ct);
        // The e-mail or subject already has an office; creating a second one would strand it.
        return provisioned is null
            ? Result<DemoRequestResponse>.Conflict("Bu hesap için zaten bir büro mevcut.")
            : Result<DemoRequestResponse>.Ok(new DemoRequestResponse(true));
    }

    public async Task<Result<DemoRequestResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var provisioned = await provisioner.ProvisionLocalAsync(
            request.Email!.Trim(), passwordHasher.Hash(request.Password!), request.OfficeName!.Trim(), ct);
        return provisioned is null
            ? Result<DemoRequestResponse>.Conflict("Bu e-posta için zaten bir hesap mevcut.")
            : Result<DemoRequestResponse>.Ok(new DemoRequestResponse(true));
    }
}
