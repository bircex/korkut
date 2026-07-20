using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>An invite an owner just created — the caller emails the raw token, never storing it.</summary>
public sealed record IssuedInvite(Guid Id, string RawToken, DateTimeOffset ExpiresAt);

/// <summary>The office/role a pending invite grants (resolved across tenants).</summary>
public sealed record InviteGrant(Guid Id, Guid OfficeId, string Email, string Role);

/// <summary>
/// Office invitations (KDM-020). An owner creates/lists/revokes invites within their
/// tenant (RLS); accepting resolves the opaque token across tenants (the sanctioned
/// BYPASSRLS resolver), creates the member and marks the invite used — single-use.
/// </summary>
public sealed class InviteStore(KorkutDbContext db, ITokenHasher tokenHasher, IClock clock)
{
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromDays(7);

    public async Task<IssuedInvite> CreateAsync(Guid officeId, Guid actorId, string email, string role, CancellationToken ct = default)
    {
        var raw = tokenHasher.NewRawToken();
        var expiresAt = clock.UtcNow + Lifetime;
        var invite = new InviteEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Email = email,
            Role = role,
            TokenHash = tokenHasher.Hash(raw),
            ExpiresAt = expiresAt,
            CreatedBy = actorId,
            CreatedAt = clock.UtcNow,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync(ct);
        return new IssuedInvite(invite.Id, raw, expiresAt);
    }

    public Task<List<InviteEntity>> ListAsync(Guid officeId, CancellationToken ct = default)
        => db.Invites.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.AcceptedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> RevokeAsync(Guid officeId, Guid inviteId, CancellationToken ct = default)
        => await db.Invites.Where(x => x.OfficeId == officeId && x.Id == inviteId && x.AcceptedAt == null)
            .ExecuteDeleteAsync(ct) > 0;

    /// <summary>Mark an invite accepted (single-use); returns false if already used/gone.</summary>
    public async Task<bool> MarkAcceptedAsync(Guid officeId, Guid inviteId, CancellationToken ct = default)
        => await db.Invites.Where(x => x.OfficeId == officeId && x.Id == inviteId && x.AcceptedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AcceptedAt, clock.UtcNow), ct) > 0;
}
