namespace Korkut.Application.Members.Responses;

/// <summary>
/// A member as the office listing shows them, including whether this row is the caller
/// (the client greys out the actions they are not allowed to take on themselves).
/// </summary>
public sealed record MemberResponse(
    Guid Id, string Email, string? FullName, string? BarRegistryNo, string Role,
    bool TwoFactorEnabled, bool IsActive, bool Bound, bool IsSelf, DateTimeOffset CreatedAt);
