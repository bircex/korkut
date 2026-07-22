using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Security;
using Korkut.Domain.Membership;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Membership;

/// <summary>
/// Office-invitation use-cases (KDM-020). An owner creates/lists/revokes invites within
/// their tenant; the raw token is minted here (hashed at rest) and returned once so the
/// caller can build the invite link. Accepting resolves the token across tenants elsewhere.
/// </summary>
public interface IInviteService
{
    Task<IssuedInvite> CreateAsync(Guid officeId, Guid actorId, string email, string role, CancellationToken ct = default);

    Task<IReadOnlyList<Invite>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<bool> RevokeAsync(Guid officeId, Guid inviteId, CancellationToken ct = default);

    /// <summary>Consume an invite (single-use); false if already used/gone.</summary>
    Task<bool> MarkAcceptedAsync(Guid officeId, Guid inviteId, CancellationToken ct = default);
}

/// <summary>Invite orchestration over the repository port; owns token generation + lifetime.</summary>
public sealed class InviteService(IInviteRepository repo, ITokenHasher tokenHasher, IClock clock) : IInviteService
{
    /// <summary>How long a fresh invite stays valid.</summary>
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(7);

    public async Task<IssuedInvite> CreateAsync(Guid officeId, Guid actorId, string email, string role, CancellationToken ct = default)
    {
        var raw = tokenHasher.NewRawToken();
        var now = clock.UtcNow;
        var expiresAt = now + Lifetime;
        var id = await repo.AddAsync(officeId, actorId, email, role, tokenHasher.Hash(raw), expiresAt, now, ct);
        return new IssuedInvite(id, raw, expiresAt);
    }

    public Task<IReadOnlyList<Invite>> ListAsync(Guid officeId, CancellationToken ct = default)
        => repo.ListAsync(officeId, ct);

    public Task<bool> RevokeAsync(Guid officeId, Guid inviteId, CancellationToken ct = default)
        => repo.RevokeAsync(officeId, inviteId, ct);

    public Task<bool> MarkAcceptedAsync(Guid officeId, Guid inviteId, CancellationToken ct = default)
        => repo.MarkAcceptedAsync(officeId, inviteId, clock.UtcNow, ct);
}
