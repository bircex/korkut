using Korkut.Application.Errors;
using Korkut.Contract.Offices;
using Korkut.Application.Onboarding.Requests;
using Korkut.Application.Onboarding.Responses;

namespace Korkut.Application.Onboarding;

/// <summary>
/// Onboarding over the provisioner port. The identity is not taken from the request body:
/// the caller passes the e-mail and subject that Google verified, which the API layer holds
/// in a signed, short-lived ticket.
/// </summary>
public sealed class OnboardingService(IOfficeProvisioner provisioner) : IOnboardingService
{
    public async Task<DemoRequestResponse> RequestOfficeAsync(
        string email, string googleSub, DemoRequestRequest request, CancellationToken ct = default)
    {
        var provisioned = await provisioner.ProvisionAsync(email, googleSub, request.OfficeName!.Trim(), ct);
        return provisioned is null
            // The e-mail or subject already has an office; creating a second one would strand it.
            ? throw new ConflictException("Bu hesap için zaten bir büro mevcut.")
            : new DemoRequestResponse(true);
    }
}
