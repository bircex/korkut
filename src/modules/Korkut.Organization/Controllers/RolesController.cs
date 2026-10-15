using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Organization.Controllers;

/// <summary>
/// The office's roles. Listing them (with the permission catalog) is for whoever assigns roles to
/// members; changing them needs roles.manage.
/// </summary>
[ApiController]
[Route("api/roles")]
public sealed class RolesController(IRoleService roles) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MembersManage)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await roles.ListAsync(ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<IActionResult> Create([FromBody] SaveRoleRequest body, CancellationToken ct)
        => (await roles.CreateAsync(body, ct)).ToActionResult(r => Created($"/api/roles/{r.Key}", r));

    [HttpPut("{key}")]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<IActionResult> Update(string key, [FromBody] SaveRoleRequest body, CancellationToken ct)
        => (await roles.UpdateAsync(key, body, ct)).ToActionResult();

    [HttpDelete("{key}")]
    [Authorize(Policy = Permissions.RolesManage)]
    public async Task<IActionResult> Remove(string key, CancellationToken ct)
        => (await roles.RemoveAsync(key, ct)).ToActionResult();
}
