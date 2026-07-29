using Korkut.Contract;
using Korkut.Contract.Deadlines;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Deadlines.Controllers;

/// <summary>Deadlines (süreler). Forwards to <see cref="IDeadlineService"/>, maps Result to HTTP.</summary>
[ApiController]
[Route("api/deadlines")]
public sealed class DeadlinesController(IDeadlineService deadlines) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => (await deadlines.ListAsync(matterId, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateDeadlineRequest body, CancellationToken ct)
        => (await deadlines.CreateAsync(body, ct)).ToActionResult(d => Created($"/api/deadlines/{d.Id}", d));

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => (await deadlines.CompleteAsync(id, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await deadlines.DeleteAsync(id, ct)).ToActionResult();
}
