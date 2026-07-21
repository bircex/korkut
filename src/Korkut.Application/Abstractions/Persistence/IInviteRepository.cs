using Korkut.Domain.Membership;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>Persistence port for office invitations (within-tenant owner operations).</summary>
public interface IInviteRepository
{
    Task<Guid> AddAsync(Guid officeId, Guid actorId, string email, string role, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt, CancellationToken ct = default);

    /// <summary>Pending (not-yet-accepted) invites, newest first.</summary>
    Task<IReadOnlyList<Invite>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<bool> RevokeAsync(Guid officeId, Guid inviteId, CancellationToken ct = default);

    /// <summary>Mark an invite accepted (single-use); false if already used/gone.</summary>
    Task<bool> MarkAcceptedAsync(Guid officeId, Guid inviteId, DateTimeOffset acceptedAt, CancellationToken ct = default);
}

/// <summary>
/// Resolves the office/role of an opaque invite token before any session exists, via the
/// narrow BYPASSRLS resolver. Null for an unknown, expired or already-accepted invite.
/// </summary>
public interface IInviteResolver
{
    Task<InviteGrant?> ResolveAsync(string tokenHash, CancellationToken ct = default);
}
