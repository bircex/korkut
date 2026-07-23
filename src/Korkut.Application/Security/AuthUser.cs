namespace Korkut.Application.Security;

/// <summary>
/// A user as the Google sign-in flow resolves them, before any tenant scope exists. The
/// local sign-in flow works on the user aggregate itself; this shape survives only for the
/// cross-tenant OIDC lookups and goes away with that slice.
/// </summary>
public sealed record AuthUser(
    Guid Id, Guid OfficeId, string Email, string? PasswordHash,
    int FailedAccessCount, DateTimeOffset? LockoutEnd, Guid SecurityStamp, int TokenEpoch);
