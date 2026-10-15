using Korkut.Contract.Matters;
using Korkut.Core;
using Korkut.Matters.Repositories;

namespace Korkut.Matters.Services;

/// <summary>
/// <see cref="IMatterAccess"/> for the caller: everything with matters.view_all, otherwise the matters
/// whose team they are on. Scoped, and computed once per request.
/// </summary>
internal sealed class MatterAccess(IMatterMemberRepository members, ICurrentUser currentUser) : IMatterAccess
{
    private MatterScope? _scope;

    public async Task<MatterScope> ScopeAsync(Guid officeId, CancellationToken ct = default)
    {
        if (_scope is not null)
        {
            return _scope;
        }

        var all = currentUser.Permissions.Contains(Permissions.MattersViewAll);
        var ids = all ? new HashSet<Guid>() : await members.MatterIdsForUserAsync(officeId, currentUser.UserId, ct);
        return _scope = new MatterScope(currentUser.UserId, all, ids);
    }

    public async Task<bool> CanManageTeamAsync(Guid officeId, Guid matterId, CancellationToken ct = default)
    {
        if (!currentUser.Permissions.Contains(Permissions.MattersManageTeam))
        {
            return false;
        }

        if (currentUser.Permissions.Contains(Permissions.MattersViewAll))
        {
            return true;
        }

        return await members.FindAsync(officeId, matterId, currentUser.UserId, ct) is { IsLead: true };
    }
}
