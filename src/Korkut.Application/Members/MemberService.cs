using Korkut.Core;
using Korkut.Application.Errors;
using Korkut.Application.Members.Requests;
using Korkut.Application.Members.Responses;
using Korkut.Application.Security;
using Korkut.Application.Users;
using Korkut.Domain.Users;

namespace Korkut.Application.Members;

/// <summary>
/// Membership orchestration over the user repository. The owner-safety rules are enforced
/// here because they are about the office as a whole, not about any one user: the last
/// owner cannot be demoted or removed, and the actor is never allowed to remove themselves.
/// </summary>
public sealed class MemberService(
    IUserRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IMemberService
{
    public Task<IReadOnlyList<MemberResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<MemberResponse>)(await repository.ListAsync(office, ct)).Select(ToResponse).ToList(), ct);

    public Task<MemberResponse> CreateAsync(CreateMemberRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            // A fresh member: unbound (no Google link yet), active, no password and no 2FA.
            var member = new User(
                office, request.Email!.Trim().ToLowerInvariant(), request.Role!,
                request.FullName?.Trim(), request.BarRegistryNo?.Trim(), clock.UtcNow);
            await repository.AddAsync(member, ct);
            return ToResponse(member);
        }, ct);

    public Task ChangeRoleAsync(Guid memberId, ChangeMemberRoleRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var member = await repository.FindForUpdateAsync(office, memberId, ct) ?? throw NotFound();
            var role = request.Role!;
            if (member.Role == Roles.OfficeOwner && role != Roles.OfficeOwner)
            {
                await EnsureAnotherOwnerAsync(office, memberId, ct);
            }

            member.ChangeRole(role);
            await repository.SaveChangesAsync(ct);
            return true;
        }, ct);

    public Task RemoveAsync(Guid memberId, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (memberId == currentUser.UserId)
            {
                throw new ForbiddenException("Kendinizi çıkaramazsınız.");
            }

            var member = await repository.FindAsync(office, memberId, ct) ?? throw NotFound();
            if (member.Role == Roles.OfficeOwner)
            {
                await EnsureAnotherOwnerAsync(office, memberId, ct);
            }

            return await repository.RemoveAsync(office, memberId, ct) ? true : throw NotFound();
        }, ct);

    /// <summary>An office with no owner left could never be administered again.</summary>
    private async Task EnsureAnotherOwnerAsync(Guid officeId, Guid excludingId, CancellationToken ct)
    {
        if (!await repository.HasAnotherOwnerAsync(officeId, excludingId, ct))
        {
            throw new ConflictException("Ofiste en az bir sahip kalmalıdır.");
        }
    }

    private static NotFoundException NotFound() => new("Üye bulunamadı.");

    private MemberResponse ToResponse(User u) =>
        new(u.Id, u.Email, u.FullName, u.BarRegistryNo, u.Role, u.TwoFactorEnabled,
            u.IsActive, u.IsBound, u.Id == currentUser.UserId, u.CreatedAt);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
