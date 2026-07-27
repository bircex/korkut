using Korkut.Application.Members.Requests;
using Korkut.Application.Members.Responses;

namespace Korkut.Application.Members;

/// <summary>
/// Office-membership use-cases, over the user aggregate viewed as the office's members.
/// Two safety rules live here: the office always keeps at least one owner, and nobody
/// removes themselves — either would leave an office nobody can administer.
/// </summary>
public interface IMemberService
{
    Task<IReadOnlyList<MemberResponse>> ListAsync(CancellationToken ct = default);

    Task<MemberResponse> CreateAsync(CreateMemberRequest request, CancellationToken ct = default);

    Task ChangeRoleAsync(Guid memberId, ChangeMemberRoleRequest request, CancellationToken ct = default);

    Task RemoveAsync(Guid memberId, CancellationToken ct = default);
}
