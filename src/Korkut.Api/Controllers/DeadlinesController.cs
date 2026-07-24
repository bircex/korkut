using Korkut.Application.Deadlines;
using Korkut.Application.Deadlines.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>Deadlines (süreler). Forwards to <see cref="IDeadlineService"/>.</summary>
[ApiController]
[Route("api/deadlines")]
public sealed class DeadlinesController(IDeadlineService deadlines) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => Ok(await deadlines.ListAsync(matterId, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateDeadlineRequest body, CancellationToken ct)
    {
        var created = await deadlines.CreateAsync(body, ct);
        return Created($"/api/deadlines/{created.Id}", created);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await deadlines.CompleteAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await deadlines.DeleteAsync(id, ct);
        return NoContent();
    }
}
