using Korkut.Core;
using Korkut.Application.Errors;
using Korkut.Organization;
using Korkut.Application.Users.Requests;
using Korkut.Application.Users.Responses;
using Korkut.Domain.Users;

namespace Korkut.Application.Users;

/// <summary>
/// The caller's own profile over the user repository. A token whose user no longer exists
/// is not an empty profile, it is a dead session, so it reads as 401.
/// </summary>
public sealed class ProfileService(
    IUserRepository users,
    IOfficeRepository offices,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IProfileService
{
    public Task<ProfileResponse> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            await ToResponseAsync(office, await FindSelfAsync(office, tracked: false, ct), ct), ct);

    public Task<ProfileResponse> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var self = await FindSelfAsync(office, tracked: true, ct);
            self.UpdateProfile(request.FullName, request.BarRegistryNo);
            await users.SaveChangesAsync(ct);
            return await ToResponseAsync(office, self, ct);
        }, ct);

    private async Task<User> FindSelfAsync(Guid officeId, bool tracked, CancellationToken ct)
        => (tracked
                ? await users.FindForUpdateAsync(officeId, currentUser.UserId, ct)
                : await users.FindAsync(officeId, currentUser.UserId, ct))
            ?? throw new UnauthorizedException("Oturum bulunamadı.");

    private async Task<ProfileResponse> ToResponseAsync(Guid officeId, User self, CancellationToken ct)
    {
        var office = await offices.FindAsync(officeId, ct);
        return new ProfileResponse(self.Email, self.Email, self.FullName, self.BarRegistryNo, office?.Name, self.Role);
    }

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
