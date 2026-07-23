using Korkut.Application.Security;
using Korkut.Application.Tasks;
using Korkut.Application.Tasks.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Tasks;

/// <summary>Office tasks (görevler). Forwards to <see cref="ITaskService"/>.</summary>
[ApiController]
[Route("api/tasks")]
public sealed class TasksController(ITaskService tasks) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => Ok(await tasks.ListAsync(matterId, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest body, CancellationToken ct)
    {
        var created = await tasks.CreateAsync(body, ct);
        return Created($"/api/tasks/{created.Id}", created);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateTaskStatusRequest body, CancellationToken ct)
        => Ok(await tasks.SetStatusAsync(id, body, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tasks.DeleteAsync(id, ct);
        return NoContent();
    }
}
