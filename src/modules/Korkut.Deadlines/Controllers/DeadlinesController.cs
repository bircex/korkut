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
    [Authorize(Policy = Permissions.AgendaRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => (await deadlines.ListAsync(matterId, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.AgendaWrite)]
    public async Task<IActionResult> Create([FromBody] CreateDeadlineRequest body, CancellationToken ct)
        => (await deadlines.CreateAsync(body, ct)).ToActionResult(d => Created($"/api/deadlines/{d.Id}", d));

    /// <summary>Nested create under a matter (the SPA posts here); the path's matter id wins.</summary>
    [HttpPost("/api/matters/{matterId:guid}/deadlines")]
    [Authorize(Policy = Permissions.AgendaWrite)]
    public async Task<IActionResult> CreateForMatter(Guid matterId, [FromBody] CreateDeadlineRequest body, CancellationToken ct)
        => (await deadlines.CreateAsync(body with { MatterId = matterId }, ct))
            .ToActionResult(d => Created($"/api/deadlines/{d.Id}", d));

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Permissions.AgendaWrite)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
        => (await deadlines.CompleteAsync(id, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.AgendaWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await deadlines.DeleteAsync(id, ct)).ToActionResult();
}
