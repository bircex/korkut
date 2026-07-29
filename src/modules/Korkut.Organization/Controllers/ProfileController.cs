using Korkut.Contract;
using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Organization.Controllers;

/// <summary>The signed-in user's own account: the session probe + the two editable profile fields.</summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class ProfileController(IProfileService profile) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await profile.GetAsync(ct)).ToActionResult();

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest body, CancellationToken ct)
        => (await profile.UpdateAsync(body, ct)).ToActionResult();
}
