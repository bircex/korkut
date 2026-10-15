using Korkut.Contract;
using Korkut.Contract.Matters;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Matters.Entities;
using Korkut.Matters.Repositories;

namespace Korkut.Matters.Services;

/// <summary>
/// A matter's team. Whoever may see the matter sees its team; changing it takes
/// matters.manage_team and either seeing every matter or being the matter's lead.
/// </summary>
internal sealed class MatterTeamService(
    IMatterRepository matters, IMatterMemberRepository members, IMatterAccess access, IUserLookup users,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IMatterTeamService
{
    private const string NotFound = "Dosya bulunamadı.";

    public Task<Result<MatterTeamResponse>> GetAsync(Guid matterId, CancellationToken ct = default)
        => InScopeAsync(async office => await VisibleAsync(office, matterId, ct)
            ? Result<MatterTeamResponse>.Ok(await DescribeAsync(office, matterId, ct))
            : Result<MatterTeamResponse>.NotFound(NotFound), ct);

    public Task<Result<IReadOnlyList<MatterTeamCandidate>>> CandidatesAsync(Guid matterId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (!await VisibleAsync(office, matterId, ct))
            {
                return Result<IReadOnlyList<MatterTeamCandidate>>.NotFound(NotFound);
            }

            if (!await access.CanManageTeamAsync(office, matterId, ct))
            {
                return Result<IReadOnlyList<MatterTeamCandidate>>.Forbidden("Bu dosyanın ekibini değiştiremezsiniz.");
            }

            var onTeam = (await members.ListForMatterAsync(office, matterId, ct)).Select(m => m.UserId).ToHashSet();
            var everyone = await users.DirectoryAsync(office, ct);
            return Result<IReadOnlyList<MatterTeamCandidate>>.Ok(
                everyone.Where(u => !onTeam.Contains(u.Key)).Select(u => new MatterTeamCandidate(u.Key, u.Value)).OrderBy(c => c.Name).ToList());
        }, ct);

    public Task<Result<MatterTeamResponse>> AddAsync(Guid matterId, AddTeamMemberRequest request, CancellationToken ct = default)
        => ChangeAsync(matterId, async office =>
        {
            if (await users.RoleAsync(office, request.UserId, ct) is null)
            {
                return Result<MatterTeamResponse>.NotFound("Üye bulunamadı.");
            }

            var existing = await members.FindAsync(office, matterId, request.UserId, ct);
            if (existing is null)
            {
                members.Add(new MatterMember(office, matterId, request.UserId, request.IsLead, currentUser.UserId, clock.UtcNow));
            }
            else
            {
                existing.SetLead(request.IsLead);
            }

            await members.SaveChangesAsync(ct);
            return Result<MatterTeamResponse>.Ok(await DescribeAsync(office, matterId, ct));
        }, ct);

    public Task<Result<MatterTeamResponse>> SetLeadAsync(Guid matterId, Guid userId, SetTeamLeadRequest request, CancellationToken ct = default)
        => ChangeAsync(matterId, async office =>
        {
            var member = await members.FindAsync(office, matterId, userId, ct);
            if (member is null)
            {
                return Result<MatterTeamResponse>.NotFound("Bu kişi dosyanın ekibinde değil.");
            }

            member.SetLead(request.IsLead);
            await members.SaveChangesAsync(ct);
            return Result<MatterTeamResponse>.Ok(await DescribeAsync(office, matterId, ct));
        }, ct);

    public Task<Result<MatterTeamResponse>> RemoveAsync(Guid matterId, Guid userId, CancellationToken ct = default)
        => ChangeAsync(matterId, async office =>
        {
            var member = await members.FindAsync(office, matterId, userId, ct);
            if (member is null)
            {
                return Result<MatterTeamResponse>.NotFound("Bu kişi dosyanın ekibinde değil.");
            }

            members.Remove(member);
            await members.SaveChangesAsync(ct);
            return Result<MatterTeamResponse>.Ok(await DescribeAsync(office, matterId, ct));
        }, ct);

    private Task<Result<MatterTeamResponse>> ChangeAsync(Guid matterId, Func<Guid, Task<Result<MatterTeamResponse>>> change, CancellationToken ct)
        => InScopeAsync(async office =>
        {
            if (!await VisibleAsync(office, matterId, ct))
            {
                return Result<MatterTeamResponse>.NotFound(NotFound);
            }

            return await access.CanManageTeamAsync(office, matterId, ct)
                ? await change(office)
                : Result<MatterTeamResponse>.Forbidden("Bu dosyanın ekibini değiştiremezsiniz.");
        }, ct);

    private async Task<bool> VisibleAsync(Guid office, Guid matterId, CancellationToken ct)
        => (await access.ScopeAsync(office, ct)).CanSee(matterId) && await matters.FindAsync(office, matterId, ct) is not null;

    private async Task<MatterTeamResponse> DescribeAsync(Guid office, Guid matterId, CancellationToken ct)
    {
        var team = await members.ListForMatterAsync(office, matterId, ct);
        var names = await users.DisplayNamesAsync(office, team.Select(m => m.UserId).ToList(), ct);
        return new MatterTeamResponse(
            team.Select(m => new MatterTeamMember(m.UserId, names.GetValueOrDefault(m.UserId, "Üye"), m.IsLead, m.AddedAt)).ToList(),
            await access.CanManageTeamAsync(office, matterId, ct));
    }

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
