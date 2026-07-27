using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

/// <summary>Body for the self-serve signup: the office the visitor wants created.</summary>
public sealed record DemoRequestRequest
{
    [Required(ErrorMessage = "Büro adı zorunludur.")]
    [MaxLength(200, ErrorMessage = "Büro adı en fazla 200 karakter olabilir.")]
    public string? OfficeName { get; init; }

    /// <summary>The requester's name (captured for the record; optional).</summary>
    [MaxLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }
}

/// <summary>The office was created and is waiting for a platform admin to enable it.</summary>
public sealed record DemoRequestResponse(bool Pending);

/// <summary>The Google-verified e-mail waiting to be turned into an office.</summary>
public sealed record SignupPendingResponse(string Email);

/// <summary>
/// Self-serve onboarding (cross-module contract): a visitor whose Google e-mail has no account asks
/// for an office. The office is created disabled until a platform admin enables it. RETURNS a Result.
/// </summary>
public interface IOnboardingService
{
    Task<Result<DemoRequestResponse>> RequestOfficeAsync(
        string email, string googleSub, DemoRequestRequest request, CancellationToken ct = default);
}
