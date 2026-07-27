using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Auth.Requests;

/// <summary>Body for setting a new password with a reset token.</summary>
public sealed record ResetPasswordRequest
{
    [Required(ErrorMessage = "Bağlantı kodu zorunludur.")]
    public string? Token { get; init; }

    [Required(ErrorMessage = "Parola zorunludur.")]
    [MinLength(10, ErrorMessage = "Parola en az 10 karakter olmalıdır.")]
    public string? Password { get; init; }
}
