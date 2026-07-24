using Korkut.Application.Correspondence;
using Korkut.Application.Correspondence.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// The correspondence register ("evrak defteri"). Reads use matters.read and writes
/// matters.write, since evrak is matter-adjacent case work.
/// </summary>
[ApiController]
[Route("api/evrak")]
public sealed class CorrespondenceController(ICorrespondenceService correspondence) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] string? direction, CancellationToken ct)
        => Ok(await correspondence.ListAsync(direction, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateCorrespondenceRequest body, CancellationToken ct)
    {
        var created = await correspondence.CreateAsync(body, ct);
        return Created($"/api/evrak/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await correspondence.DeleteAsync(id, ct);
        return NoContent();
    }
}
