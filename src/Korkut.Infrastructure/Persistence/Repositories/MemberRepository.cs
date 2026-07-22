using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Security;
using Korkut.Domain.Membership;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IMemberRepository"/>; maps UserEntity ↔ Member domain model.</summary>
public sealed class MemberRepository(KorkutDbContext db) : IMemberRepository
{
    public async Task<IReadOnlyList<Member>> ListAsync(Guid officeId, CancellationToken ct = default)
        => (await db.Users.AsNoTracking().Where(x => x.OfficeId == officeId).OrderBy(x => x.Email).ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<Member?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var e = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Id == id, ct);
        return e is null ? null : ToDomain(e);
    }

    public async Task<Member> AddAsync(Member member, CancellationToken ct = default)
    {
        db.Users.Add(new UserEntity
        {
            Id = member.Id, OfficeId = member.OfficeId, Email = member.Email, Role = member.Role,
            FullName = member.FullName, BarRegistryNo = member.BarRegistryNo,
            SecurityStamp = UuidV7.NewId(), CreatedAt = member.CreatedAt,
        });
        await db.SaveChangesAsync(ct);
        return member;
    }

    public async Task<bool> SetRoleAsync(Guid officeId, Guid id, string role, CancellationToken ct = default)
    {
        // Bump the security stamp so the member's existing sessions are revoked after the change.
        var stamp = UuidV7.NewId();
        return await db.Users.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Role, role).SetProperty(x => x.SecurityStamp, stamp), ct) > 0;
    }

    public async Task<bool> RemoveAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.Users.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    public Task<bool> HasAnotherOwnerAsync(Guid officeId, Guid excludingId, CancellationToken ct = default)
        => db.Users.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.Role == Roles.OfficeOwner && x.Id != excludingId)
            .AnyAsync(ct);

    private static Member ToDomain(UserEntity u) =>
        new(u.Id, u.OfficeId, u.Email, u.FullName, u.BarRegistryNo, u.Role, u.TwoFactorEnabled,
            u.GoogleSub != null, u.LockoutEnd == null, u.CreatedAt);
}
