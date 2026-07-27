using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Members.Requests;

/// <summary>Body for adding a member to the office.</summary>
public sealed record CreateMemberRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Rol zorunludur.")]
    [RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }

    [MaxLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }

    [MaxLength(50, ErrorMessage = "Baro sicil no en fazla 50 karakter olabilir.")]
    public string? BarRegistryNo { get; init; }
}
