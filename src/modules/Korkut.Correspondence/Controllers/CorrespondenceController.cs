using Korkut.Contract;
using Korkut.Contract.Correspondence;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Correspondence.Controllers;

/// <summary>
/// The correspondence register ("evrak defteri"). Reads use matters.read and writes matters.write,
/// since evrak is matter-adjacent case work. Maps Result to HTTP.
/// </summary>
[ApiController]
[Route("api/evrak")]
public sealed class CorrespondenceController(ICorrespondenceService correspondence) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.NotesRead)]
    public async Task<IActionResult> List([FromQuery] string? direction, CancellationToken ct)
        => (await correspondence.ListAsync(direction, ct)).ToListActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.NotesWrite)]
    public async Task<IActionResult> Create([FromBody] CreateCorrespondenceRequest body, CancellationToken ct)
        => (await correspondence.CreateAsync(body, ct)).ToActionResult(c => Created($"/api/evrak/{c.Id}", c));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.NotesWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await correspondence.DeleteAsync(id, ct)).ToActionResult();
}
