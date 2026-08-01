using Korkut.Core;
using Korkut.Contract;
using Korkut.Contract.Organization;
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
public sealed class InvitesController(IInviteService invites, Korkut.Api.Auth.SessionManager session) : ControllerBase
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

    /// <summary>Katılım ekranı için davet bilgisi — token TÜKETİLMEZ.</summary>
    [HttpGet("peek")]
    [AllowAnonymous]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> Peek([FromQuery] string? token, CancellationToken ct)
        => string.IsNullOrWhiteSpace(token)
            ? throw Korkut.Web.Errors.ApiException.BadRequest("'token' gereklidir.")
            : (await invites.PeekAsync(token, ct)).ToActionResult();

    /// <summary>Parola belirleyerek katılım: hesabı açar, daveti tüketir ve oturumu BAŞLATIR —
    /// davetli, davet edenin bürosunun üyesi olarak doğrudan uygulamaya düşer.</summary>
    [HttpPost("accept-local")]
    [AllowAnonymous]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> AcceptLocal([FromBody] AcceptInviteLocalRequest body, CancellationToken ct)
    {
        var result = await invites.AcceptLocalAsync(body, ct);
        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        var user = result.Value!;
        await session.IssueAsync(Response, user.OfficeId, user.UserId, user.Role, user.SecurityStamp, ct);
        return Ok(new InviteAcceptedResponse(true, user.Email));
    }
}
