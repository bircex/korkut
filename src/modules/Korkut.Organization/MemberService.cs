using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization;

/// <summary>
/// Membership orchestration over the user repository. The owner-safety rules live here because they
/// are about the office as a whole: the last owner cannot be demoted or removed, and the actor is
/// never allowed to remove themselves. Every call RETURNS a <see cref="Result"/>.
/// </summary>
public sealed class MemberService(
    IUserRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IMemberService
{
    public Task<Result<IReadOnlyList<MemberResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<MemberResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<MemberResponse>> CreateAsync(CreateMemberRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var member = new User(
                office, request.Email!.Trim().ToLowerInvariant(), request.Role!,
                request.FullName?.Trim(), request.BarRegistryNo?.Trim(), clock.UtcNow);
            await repository.AddAsync(member, ct);
            return Result<MemberResponse>.Ok(ToResponse(member));
        }, ct);

    public Task<Result> ChangeRoleAsync(Guid memberId, ChangeMemberRoleRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var member = await repository.FindForUpdateAsync(office, memberId, ct);
            if (member is null)
            {
                return Result.NotFound("Üye bulunamadı.");
            }

            var role = request.Role!;
            if (member.Role == Roles.OfficeOwner && role != Roles.OfficeOwner
                && !await repository.HasAnotherOwnerAsync(office, memberId, ct))
            {
                return Result.Conflict("Ofiste en az bir sahip kalmalıdır.");
            }

            member.ChangeRole(role);
            await repository.SaveChangesAsync(ct);
            return Result.Ok();
        }, ct);

    public Task<Result> RemoveAsync(Guid memberId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (memberId == currentUser.UserId)
            {
                return Result.Forbidden("Kendinizi çıkaramazsınız.");
            }

            var member = await repository.FindAsync(office, memberId, ct);
            if (member is null)
            {
                return Result.NotFound("Üye bulunamadı.");
            }

            if (member.Role == Roles.OfficeOwner && !await repository.HasAnotherOwnerAsync(office, memberId, ct))
            {
                return Result.Conflict("Ofiste en az bir sahip kalmalıdır.");
            }

            return await repository.RemoveAsync(office, memberId, ct) ? Result.Ok() : Result.NotFound("Üye bulunamadı.");
        }, ct);

    private MemberResponse ToResponse(User u) =>
        new(u.Id, u.Email, u.FullName, u.BarRegistryNo, u.Role, u.TwoFactorEnabled,
            u.IsActive, u.IsBound, u.Id == currentUser.UserId, u.CreatedAt);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
