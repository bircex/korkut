namespace Korkut.Domain.Membership;

/// <summary>A pending office invitation (domain model) as shown in an owner's listing.</summary>
public sealed record Invite(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

/// <summary>An invite an owner just created — the caller emails the raw token, never storing it.</summary>
public sealed record IssuedInvite(Guid Id, string RawToken, DateTimeOffset ExpiresAt);

/// <summary>The office/role a pending invite grants (resolved across tenants).</summary>
public sealed record InviteGrant(Guid Id, Guid OfficeId, string Email, string Role);
