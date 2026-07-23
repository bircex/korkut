using Korkut.Application.Security;
using Korkut.Application.Settings;
using Korkut.Application.Settings.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Settings;

/// <summary>
/// The office memory list. Any case-worker may propose an item (matters.write); approving
/// and removing items curates what the assistant relies on, so both need office.settings.
/// </summary>
[ApiController]
[Route("api/memory")]
public sealed class MemoryController(IMemoryService memory) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await memory.ListAsync(ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Add([FromBody] CreateMemoryRequest body, CancellationToken ct)
    {
        var created = await memory.AddAsync(body, ct);
        return Created($"/api/memory/{created.Id}", created);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => Ok(await memory.ApproveAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await memory.DeleteAsync(id, ct);
        return NoContent();
    }
}
