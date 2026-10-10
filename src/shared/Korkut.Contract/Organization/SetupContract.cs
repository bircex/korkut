using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

/// <summary>Whether this install still needs its first-run setup (no office yet).</summary>
public sealed record SetupStatusResponse(bool SetupRequired);

/// <summary>First-run setup: the office and the owner account that will run this install.</summary>
public sealed record SetupRequest
{
    [Required(ErrorMessage = "Büro adı zorunludur.")]
    [MaxLength(200, ErrorMessage = "Büro adı en fazla 200 karakter olabilir.")]
    public string? OfficeName { get; init; }

    [Required(ErrorMessage = "Ad Soyad zorunludur.")]
    [MaxLength(200, ErrorMessage = "Ad Soyad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [MaxLength(320)]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Parola zorunludur.")]
    [MinLength(10, ErrorMessage = "Parola en az 10 karakter olmalıdır.")]
    [MaxLength(200)]
    public string? Password { get; init; }
}

/// <summary>The office and owner were created; the owner signs in with the same e-mail and password.</summary>
public sealed record SetupResponse(Guid OfficeId);

public interface ISetupService
{
    Task<SetupStatusResponse> StatusAsync(CancellationToken ct = default);

    /// <summary>Creates the install's office (enabled) and its owner. Conflict once an office exists.</summary>
    Task<Result<SetupResponse>> SetupAsync(SetupRequest request, CancellationToken ct = default);
}
