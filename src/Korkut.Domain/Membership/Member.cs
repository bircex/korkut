namespace Korkut.Domain.Membership;

/// <summary>
/// An office member (domain model). <see cref="Bound"/> is true once the person has
/// completed a first Google sign-in (their account is linked); <see cref="IsActive"/> is
/// false while the account is locked out. Both are derived from the underlying user.
/// </summary>
public sealed record Member(
    Guid Id,
    Guid OfficeId,
    string Email,
    string? FullName,
    string? BarRegistryNo,
    string Role,
    bool TwoFactorEnabled,
    bool Bound,
    bool IsActive,
    DateTimeOffset CreatedAt);
