using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Auth.Requests;

/// <summary>Body for a local (e-mail + password) sign-in.</summary>
public sealed record LoginRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Parola zorunludur.")]
    public string? Password { get; init; }
}
