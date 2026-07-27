using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Auth.Requests;

/// <summary>Body for asking for a password-reset link.</summary>
public sealed record RequestPasswordResetRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }
}
