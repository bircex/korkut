namespace Korkut.Application.Onboarding.Responses;

/// <summary>The Google-verified e-mail waiting to be turned into an office.</summary>
public sealed record SignupPendingResponse(string Email);
