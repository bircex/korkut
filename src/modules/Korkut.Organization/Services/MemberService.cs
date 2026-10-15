using Korkut.Organization.Services;
using Korkut.Organization.Repositories;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract;
using Korkut.Contract.Audit;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization.Services;

/// <summary>
/// Membership orchestration over the user repository. The owner-safety rules live here because they
/// are about the office as a whole: the last owner cannot be demoted or removed, and the actor is
/// never allowed to remove themselves. Every call RETURNS a <see cref="Result"/>.
/// </summary>
public sealed class MemberService(
    IUserRepository repository, IRoleService roles, IUnitOfWork unitOfWork, ICurrentUser currentUser, IAuditLog audit,
    IClock clock) : IMemberService
{
    public Task<Result<IReadOnlyList<MemberResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<MemberResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<MemberResponse>> CreateAsync(CreateMemberRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var role = request.Role ?? Roles.Lawyer;
            if (!await roles.ExistsAsync(office, role, ct))
            {
                return Result<MemberResponse>.Unprocessable("Böyle bir rol yok.");
            }

            var member = new User(
                office, request.Email!.Trim().ToLowerInvariant(), role,
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
            if (!await roles.ExistsAsync(office, role, ct))
            {
                return Result.Unprocessable("Böyle bir rol yok.");
            }

            if (member.Role == Roles.OfficeOwner && role != Roles.OfficeOwner
                && !await repository.HasAnotherOwnerAsync(office, memberId, ct))
            {
                return Result.Conflict("Büroda en az bir yönetici kalmalıdır.");
            }

            var before = member.Role;
            member.ChangeRole(role);
            await repository.SaveChangesAsync(ct);
            await audit.WriteAsync(new AuditEntry(
                office, currentUser.UserId, null, null, AuditActions.MemberRoleChanged, "user", memberId,
                new { role = before }, new { role }), ct);
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
                return Result.Conflict("Büroda en az bir yönetici kalmalıdır.");
            }

            if (!await repository.RemoveAsync(office, memberId, ct))
            {
                return Result.NotFound("Üye bulunamadı.");
            }

            await audit.WriteAsync(new AuditEntry(
                office, currentUser.UserId, null, null, AuditActions.MemberRemoved, "user", memberId, new { role = member.Role }, null), ct);
            return Result.Ok();
        }, ct);

    private MemberResponse ToResponse(User u) =>
        new(u.Id, u.Email, u.Email, u.FullName, u.BarRegistryNo, u.Role, u.TwoFactorEnabled,
            u.IsActive, u.PasswordHash is not null, u.Id == currentUser.UserId, u.CreatedAt);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
