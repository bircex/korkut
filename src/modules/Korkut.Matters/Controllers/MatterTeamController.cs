using Korkut.Contract;
using Korkut.Contract.Matters;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Matters.Controllers;

/// <summary>A matter's team. Reading it needs the matter; changing it needs matters.manage_team (and lead or all-matters access).</summary>
[ApiController]
[Route("api/matters/{matterId:guid}/team")]
public sealed class MatterTeamController(IMatterTeamService team) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(Guid matterId, CancellationToken ct)
        => (await team.GetAsync(matterId, ct)).ToActionResult();

    [HttpGet("candidates")]
    [Authorize(Policy = Permissions.MattersManageTeam)]
    public async Task<IActionResult> Candidates(Guid matterId, CancellationToken ct)
        => (await team.CandidatesAsync(matterId, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.MattersManageTeam)]
    public async Task<IActionResult> Add(Guid matterId, [FromBody] AddTeamMemberRequest body, CancellationToken ct)
        => (await team.AddAsync(matterId, body, ct)).ToActionResult();

    [HttpPatch("{userId:guid}")]
    [Authorize(Policy = Permissions.MattersManageTeam)]
    public async Task<IActionResult> SetLead(Guid matterId, Guid userId, [FromBody] SetTeamLeadRequest body, CancellationToken ct)
        => (await team.SetLeadAsync(matterId, userId, body, ct)).ToActionResult();

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = Permissions.MattersManageTeam)]
    public async Task<IActionResult> Remove(Guid matterId, Guid userId, CancellationToken ct)
        => (await team.RemoveAsync(matterId, userId, ct)).ToActionResult();
}
