using Korkut.Domain.Membership;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>Persistence port for office members (the office's users, viewed as members).</summary>
public interface IMemberRepository
{
    Task<IReadOnlyList<Member>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<Member?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Member> AddAsync(Member member, CancellationToken ct = default);

    /// <summary>Change a member's role AND bump its security stamp (revokes existing sessions). False if missing.</summary>
    Task<bool> SetRoleAsync(Guid officeId, Guid id, string role, CancellationToken ct = default);

    Task<bool> RemoveAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>True if another owner exists (used to enforce the office-keeps-an-owner rule).</summary>
    Task<bool> HasAnotherOwnerAsync(Guid officeId, Guid excludingId, CancellationToken ct = default);
}
