using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Membership;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="IInviteRepository"/>. Within-tenant owner operations
/// (RLS); accepting is resolved across tenants by <see cref="IInviteResolver"/>. Only the
/// token hash is stored, never the raw token.
/// </summary>
public sealed class InviteRepository(KorkutDbContext db) : IInviteRepository
{
    public async Task<Guid> AddAsync(Guid officeId, Guid actorId, string email, string role, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt, CancellationToken ct = default)
    {
        var invite = new InviteEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Email = email,
            Role = role,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedBy = actorId,
            CreatedAt = createdAt,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync(ct);
        return invite.Id;
    }

    public async Task<IReadOnlyList<Invite>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await db.Invites.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.AcceptedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new Invite(x.Id, x.Email, x.Role, x.ExpiresAt, x.CreatedAt))
            .ToListAsync(ct);

    public async Task<bool> RevokeAsync(Guid officeId, Guid inviteId, CancellationToken ct = default)
        => await db.Invites.Where(x => x.OfficeId == officeId && x.Id == inviteId && x.AcceptedAt == null)
            .ExecuteDeleteAsync(ct) > 0;

    public async Task<bool> MarkAcceptedAsync(Guid officeId, Guid inviteId, DateTimeOffset acceptedAt, CancellationToken ct = default)
        => await db.Invites.Where(x => x.OfficeId == officeId && x.Id == inviteId && x.AcceptedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AcceptedAt, acceptedAt), ct) > 0;
}
