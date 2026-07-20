using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>Outcome of a member mutation (so the API can map safe status codes).</summary>
public enum MemberMutation
{
    Ok,
    NotFound,
    LastOwner,
    SelfForbidden,
}

/// <summary>
/// Office membership management (KDM-020 authorization boundary). Members are the
/// office's users; role changes and removals go through here. Two invariants protect
/// the office: an office always keeps at least one owner, and an admin can't remove or
/// demote themselves out of that safety. Tenant context is set by the caller (RLS).
/// </summary>
public sealed class MembersStore(KorkutDbContext db, IClock clock)
{
    public Task<List<UserEntity>> ListAsync(Guid officeId, CancellationToken ct = default)
        => db.Users.AsNoTracking()
            .Where(x => x.OfficeId == officeId)
            .OrderBy(x => x.Email)
            .ToListAsync(ct);

    public async Task<Guid> CreateAsync(Guid officeId, string email, string role, CancellationToken ct = default)
    {
        var user = new UserEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Email = email,
            Role = role,
            SecurityStamp = UuidV7.NewId(),
            CreatedAt = clock.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task<MemberMutation> ChangeRoleAsync(Guid officeId, Guid actorId, Guid memberId, string newRole, CancellationToken ct = default)
    {
        var member = await db.Users.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == memberId, ct);
        if (member is null)
        {
            return MemberMutation.NotFound;
        }

        // Demoting the last owner would lock the office out of owner-only actions.
        if (member.Role == Roles.OfficeOwner && newRole != Roles.OfficeOwner && await IsLastOwnerAsync(officeId, memberId, ct))
        {
            return MemberMutation.LastOwner;
        }

        // Bumping the security stamp revokes the member's existing sessions after a
        // privilege change.
        member.Role = newRole;
        member.SecurityStamp = UuidV7.NewId();
        await db.SaveChangesAsync(ct);
        return MemberMutation.Ok;
    }

    public async Task<MemberMutation> RemoveAsync(Guid officeId, Guid actorId, Guid memberId, CancellationToken ct = default)
    {
        if (memberId == actorId)
        {
            return MemberMutation.SelfForbidden;
        }

        var member = await db.Users.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == memberId, ct);
        if (member is null)
        {
            return MemberMutation.NotFound;
        }

        if (member.Role == Roles.OfficeOwner && await IsLastOwnerAsync(officeId, memberId, ct))
        {
            return MemberMutation.LastOwner;
        }

        db.Users.Remove(member);
        await db.SaveChangesAsync(ct);
        return MemberMutation.Ok;
    }

    private async Task<bool> IsLastOwnerAsync(Guid officeId, Guid excludingId, CancellationToken ct)
    {
        var anotherOwnerExists = await db.Users.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.Role == Roles.OfficeOwner && x.Id != excludingId)
            .AnyAsync(ct);
        return !anotherOwnerExists;
    }
}
