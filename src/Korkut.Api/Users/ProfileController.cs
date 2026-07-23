using Korkut.Application.Users;
using Korkut.Application.Users.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Users;

/// <summary>
/// The signed-in user's own account: the session probe the client calls on every load, and
/// the two profile fields they may change about themselves.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class ProfileController(IProfileService profile) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await profile.GetAsync(ct));

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest body, CancellationToken ct)
        => Ok(await profile.UpdateAsync(body, ct));
}
