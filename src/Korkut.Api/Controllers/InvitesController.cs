using Korkut.Core;
using Korkut.Application.Invites;
using Korkut.Application.Invites.Requests;
using Korkut.Application.Security;
using Korkut.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>
/// Office invitations. Owners manage them inside their own tenant (members.manage);
/// accepting is anonymous — the token names the office — and rate-limited, since it is
/// reachable without a session.
/// </summary>
[ApiController]
[Route("api/invites")]
public sealed class InvitesController(IInviteService invites) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MembersManage)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await invites.ListAsync(ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MembersManage)]
    public async Task<IActionResult> Create([FromBody] CreateInviteRequest body, CancellationToken ct)
    {
        var created = await invites.CreateAsync(body, ct);
        return Created($"/api/invites/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MembersManage)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await invites.RevokeAsync(id, ct);
        return NoContent();
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> Accept([FromBody] AcceptInviteRequest body, CancellationToken ct)
        => Ok(await invites.AcceptAsync(body, ct));
}
