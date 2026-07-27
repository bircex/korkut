using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Core;

namespace Korkut.Organization;

/// <summary>
/// The caller's own profile over the user repository. A token whose user no longer exists is not an
/// empty profile, it is a dead session, so it reads as Unauthorized. Every call RETURNS a Result.
/// </summary>
public sealed class ProfileService(
    IUserRepository users, IOfficeRepository offices, IUnitOfWork unitOfWork, ICurrentUser currentUser) : IProfileService
{
    public Task<Result<ProfileResponse>> GetAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var self = await users.FindAsync(office, currentUser.UserId, ct);
            return self is null ? Unauthorized() : Result<ProfileResponse>.Ok(await ToResponseAsync(office, self, ct));
        }, ct);

    public Task<Result<ProfileResponse>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var self = await users.FindForUpdateAsync(office, currentUser.UserId, ct);
            if (self is null)
            {
                return Unauthorized();
            }

            self.UpdateProfile(request.FullName, request.BarRegistryNo);
            await users.SaveChangesAsync(ct);
            return Result<ProfileResponse>.Ok(await ToResponseAsync(office, self, ct));
        }, ct);

    private static Result<ProfileResponse> Unauthorized() => Result<ProfileResponse>.Unauthorized("Oturum bulunamadı.");

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
