using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Security;
using Korkut.Domain.Membership;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Membership;

/// <summary>Outcome of a member mutation, mapped to safe HTTP status codes by the API.</summary>
public enum MemberMutation
{
    Ok,
    NotFound,
    LastOwner,
    SelfForbidden,
}

/// <summary>
/// Office-membership use-cases. Owns the two safety invariants — the office always keeps
/// at least one owner, and an admin can't remove/demote themselves out of that safety.
/// </summary>
public interface IMembersService
{
    Task<IReadOnlyList<Member>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<Member> CreateAsync(Guid officeId, string email, string role, string? fullName, string? barRegistryNo, CancellationToken ct = default);

    Task<MemberMutation> ChangeRoleAsync(Guid officeId, Guid actorId, Guid memberId, string newRole, CancellationToken ct = default);

    Task<MemberMutation> RemoveAsync(Guid officeId, Guid actorId, Guid memberId, CancellationToken ct = default);
}

/// <summary>Membership orchestration over the repository port; enforces the owner-safety rules.</summary>
public sealed class MembersService(IMemberRepository repo, IClock clock) : IMembersService
{
    public Task<IReadOnlyList<Member>> ListAsync(Guid officeId, CancellationToken ct = default)
        => repo.ListAsync(officeId, ct);

    public Task<Member> CreateAsync(Guid officeId, string email, string role, string? fullName, string? barRegistryNo, CancellationToken ct = default)
    {
        // A fresh member: unbound (no Google link yet), active, no 2FA.
        var member = new Member(UuidV7.NewId(), officeId, email, fullName, barRegistryNo, role, false, false, true, clock.UtcNow);
        return repo.AddAsync(member, ct);
    }

    public async Task<MemberMutation> ChangeRoleAsync(Guid officeId, Guid actorId, Guid memberId, string newRole, CancellationToken ct = default)
    {
        var member = await repo.GetAsync(officeId, memberId, ct);
        if (member is null)
        {
            return MemberMutation.NotFound;
        }

        // Demoting the last owner would lock the office out of owner-only actions.
        if (member.Role == Roles.OfficeOwner && newRole != Roles.OfficeOwner && !await repo.HasAnotherOwnerAsync(officeId, memberId, ct))
        {
            return MemberMutation.LastOwner;
        }

        return await repo.SetRoleAsync(officeId, memberId, newRole, ct) ? MemberMutation.Ok : MemberMutation.NotFound;
    }

    public async Task<MemberMutation> RemoveAsync(Guid officeId, Guid actorId, Guid memberId, CancellationToken ct = default)
    {
        if (memberId == actorId)
        {
            return MemberMutation.SelfForbidden;
        }

        var member = await repo.GetAsync(officeId, memberId, ct);
        if (member is null)
        {
            return MemberMutation.NotFound;
        }

        if (member.Role == Roles.OfficeOwner && !await repo.HasAnotherOwnerAsync(officeId, memberId, ct))
        {
            return MemberMutation.LastOwner;
        }

        return await repo.RemoveAsync(officeId, memberId, ct) ? MemberMutation.Ok : MemberMutation.NotFound;
    }
}
