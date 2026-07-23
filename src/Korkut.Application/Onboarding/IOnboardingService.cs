using Korkut.Application.Onboarding.Requests;
using Korkut.Application.Onboarding.Responses;

namespace Korkut.Application.Onboarding;

/// <summary>
/// Self-serve onboarding: a visitor whose Google e-mail has no account asks for an office.
/// The office is created disabled and stays that way until a platform admin enables it, so
/// this endpoint can never hand anybody access.
/// </summary>
public interface IOnboardingService
{
    Task<DemoRequestResponse> RequestOfficeAsync(
        string email, string googleSub, DemoRequestRequest request, CancellationToken ct = default);
}
