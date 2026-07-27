using Korkut.Core;
using Korkut.Contract.Organization;
using Korkut.Web;
using Korkut.Web.Kernel;
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
        => (await invites.ListAsync(ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.MembersManage)]
    public async Task<IActionResult> Create([FromBody] CreateInviteRequest body, CancellationToken ct)
        => (await invites.CreateAsync(body, ct)).ToActionResult(c => Created($"/api/invites/{c.Id}", c));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MembersManage)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => (await invites.RevokeAsync(id, ct)).ToActionResult();

    [HttpPost("accept")]
    [AllowAnonymous]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> Accept([FromBody] AcceptInviteRequest body, CancellationToken ct)
        => (await invites.AcceptAsync(body, ct)).ToActionResult();
}
