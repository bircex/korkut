using Korkut.Contract.Tasks;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Tasks;

/// <summary>Office tasks (görevler). Forwards to <see cref="ITaskService"/>, maps Result to HTTP.</summary>
[ApiController]
[Route("api/tasks")]
public sealed class TasksController(ITaskService tasks) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => (await tasks.ListAsync(matterId, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest body, CancellationToken ct)
        => (await tasks.CreateAsync(body, ct)).ToActionResult(t => Created($"/api/tasks/{t.Id}", t));

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateTaskStatusRequest body, CancellationToken ct)
        => (await tasks.SetStatusAsync(id, body, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await tasks.DeleteAsync(id, ct)).ToActionResult();
}
