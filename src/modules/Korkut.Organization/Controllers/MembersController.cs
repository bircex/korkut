using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Organization.Controllers;

/// <summary>
/// Office membership. Every action needs members.manage (owner-only); the service enforces the
/// office-keeps-an-owner (409) and no-self-removal (403) invariants. Maps Result to HTTP.
/// </summary>
[ApiController]
[Route("api/members")]
[Authorize(Policy = Permissions.MembersManage)]
public sealed class MembersController(IMemberService members) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await members.ListAsync(ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest body, CancellationToken ct)
        => (await members.CreateAsync(body, ct)).ToActionResult(m => Created($"/api/members/{m.Id}", m));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeMemberRoleRequest body, CancellationToken ct)
        => (await members.ChangeRoleAsync(id, body, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
        => (await members.RemoveAsync(id, ct)).ToActionResult();
}
