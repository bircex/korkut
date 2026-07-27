using Korkut.Contract.Settings;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Settings;

/// <summary>
/// The office memory list. Any case-worker may propose an item (matters.write); approving and
/// removing items curates what the assistant relies on, so both need office.settings.
/// </summary>
[ApiController]
[Route("api/memory")]
public sealed class MemoryController(IMemoryService memory) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await memory.ListAsync(ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Add([FromBody] CreateMemoryRequest body, CancellationToken ct)
        => (await memory.AddAsync(body, ct)).ToActionResult(m => Created($"/api/memory/{m.Id}", m));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
        => (await memory.ApproveAsync(id, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await memory.DeleteAsync(id, ct)).ToActionResult();
}
