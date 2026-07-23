using Korkut.Application.Members;
using Korkut.Application.Members.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Members;

/// <summary>
/// Office membership. Every action needs the office-scoped members.manage permission
/// (owner-only); the service enforces the office-keeps-an-owner and no-self-removal
/// invariants and those surface as 409 and 403.
/// </summary>
[ApiController]
[Route("api/members")]
[Authorize(Policy = Permissions.MembersManage)]
public sealed class MembersController(IMemberService members) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await members.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest body, CancellationToken ct)
    {
        var created = await members.CreateAsync(body, ct);
        return Created($"/api/members/{created.Id}", created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeMemberRoleRequest body, CancellationToken ct)
    {
        await members.ChangeRoleAsync(id, body, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await members.RemoveAsync(id, ct);
        return NoContent();
    }
}
