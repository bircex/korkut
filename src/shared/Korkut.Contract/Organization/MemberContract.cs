using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

/// <summary>A member as the office listing shows them, including whether this row is the caller.</summary>
public sealed record MemberResponse(
    Guid Id, string Username, string Email, string? FullName, string? BarRegistryNo, string Role,
    bool TwoFactorEnabled, bool IsActive, bool Bound, bool IsSelf, DateTimeOffset CreatedAt);

/// <summary>Body for adding a member to the office.</summary>
public sealed record CreateMemberRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    /// <summary>Display handle from the UI form; the office identity is the e-mail, so this is not persisted
    /// (Username mirrors the e-mail, matching the profile contract). Accepted so the form body validates.</summary>
    [MaxLength(200)]
    public string? Username { get; init; }

    /// <summary>Optional; defaults to "lawyer" when the form omits it.</summary>
    [RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }

    [MaxLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }

    [MaxLength(50, ErrorMessage = "Baro sicil no en fazla 50 karakter olabilir.")]
    public string? BarRegistryNo { get; init; }
}

/// <summary>Body for changing a member's role.</summary>
public sealed record ChangeMemberRoleRequest
{
    [Required(ErrorMessage = "Rol zorunludur.")]
    [RegularExpression("owner|lawyer", ErrorMessage = "role 'owner' veya 'lawyer' olmalıdır.")]
    public string? Role { get; init; }
}

/// <summary>
/// Office-membership use-cases (cross-module contract). Two safety rules: the office always keeps at
/// least one owner, and nobody removes themselves. Calls RETURN a <see cref="Result"/>.
/// </summary>
public interface IMemberService
{
    Task<Result<IReadOnlyList<MemberResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result<MemberResponse>> CreateAsync(CreateMemberRequest request, CancellationToken ct = default);

    Task<Result> ChangeRoleAsync(Guid memberId, ChangeMemberRoleRequest request, CancellationToken ct = default);

    Task<Result> RemoveAsync(Guid memberId, CancellationToken ct = default);
}
