using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Members.Requests;

/// <summary>Body for changing a member's role.</summary>
public sealed record ChangeMemberRoleRequest
{
    [Required(ErrorMessage = "Rol zorunludur.")]
    [RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}
