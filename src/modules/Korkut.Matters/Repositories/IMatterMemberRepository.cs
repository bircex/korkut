using Korkut.Matters.Entities;

namespace Korkut.Matters.Repositories;

/// <summary>Persistence port for matter teams; runs in the caller's tenant scope.</summary>
public interface IMatterMemberRepository
{
    Task<IReadOnlyList<MatterMember>> ListForMatterAsync(Guid officeId, Guid matterId, CancellationToken ct = default);

    /// <summary>The matters whose team this member is on.</summary>
    Task<IReadOnlySet<Guid>> MatterIdsForUserAsync(Guid officeId, Guid userId, CancellationToken ct = default);

    /// <summary>Tracked, for mutation.</summary>
    Task<MatterMember?> FindAsync(Guid officeId, Guid matterId, Guid userId, CancellationToken ct = default);

    void Add(MatterMember member);

    void Remove(MatterMember member);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
