using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Invites.Requests;

/// <summary>Body for inviting someone to the office.</summary>
public sealed record CreateInviteRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Rol zorunludur.")]
    [RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}
