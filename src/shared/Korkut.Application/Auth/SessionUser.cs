namespace Korkut.Application.Auth;

/// <summary>
/// What a session has to carry about its owner: who they are, what they may do, and the
/// stamp that stops being valid the moment either of those changes.
/// </summary>
public sealed record SessionUser(Guid OfficeId, Guid UserId, string Role, Guid SecurityStamp, bool TwoFactorEnabled);
