using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Contract.Organization;

namespace Korkut.Organization.Services;

/// <summary>
/// Onboarding over the provisioner port. The identity is not taken from the request body: the caller
/// passes the e-mail and subject Google verified (held in a signed, short-lived ticket by the host).
/// </summary>
public sealed class OnboardingService(IOfficeProvisioner provisioner) : IOnboardingService
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
}
