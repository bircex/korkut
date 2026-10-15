using Korkut.Matters.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Matters.Repositories;

/// <summary>EF/Postgres adapter for <see cref="IMatterMemberRepository"/> over the ambient DbContext.</summary>
internal sealed class MatterMemberRepository(DbContext db) : IMatterMemberRepository
{
    public async Task<IReadOnlyList<MatterMember>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default)
        => await db.Set<MatterMember>().AsNoTracking()
            .Where(m => m.OfficeId == officeId && m.MatterId == matterId)
            .OrderByDescending(m => m.IsLead).ThenBy(m => m.AddedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> MatterIdsForUserAsync(Guid officeId, Guid userId, CancellationToken ct = default)
        => (await db.Set<MatterMember>().AsNoTracking()
            .Where(m => m.OfficeId == officeId && m.UserId == userId)
            .Select(m => m.MatterId)
            .ToListAsync(ct)).ToHashSet();

    public Task<MatterMember?> FindAsync(Guid officeId, Guid matterId, Guid userId, CancellationToken ct = default)
        => db.Set<MatterMember>().SingleOrDefaultAsync(m => m.OfficeId == officeId && m.MatterId == matterId && m.UserId == userId, ct);

    public void Add(MatterMember member) => db.Set<MatterMember>().Add(member);

    public void Remove(MatterMember member) => db.Set<MatterMember>().Remove(member);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
