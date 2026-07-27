using Korkut.Application.Users.Requests;
using Korkut.Application.Users.Responses;

namespace Korkut.Application.Users;

/// <summary>
/// The caller's own account: who the session belongs to, and the two fields they may
/// change about themselves. Never takes a user id — it is always the caller.
/// </summary>
public interface IProfileService
{
    Task<ProfileResponse> GetAsync(CancellationToken ct = default);

    Task<ProfileResponse> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);
}
