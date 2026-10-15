using Korkut.Contract;
using Korkut.Contract.Tasks;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Tasks.Controllers;

/// <summary>Office agenda records (görev / hatırlatma / randevu). Forwards to
/// <see cref="ITaskService"/>, maps Result to HTTP.</summary>
[ApiController]
[Route("api/tasks")]
public sealed class TasksController(ITaskService tasks) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.AgendaRead)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? matterId, [FromQuery] string? status, [FromQuery] string? category, CancellationToken ct)
        => (await tasks.ListAsync(matterId, status, category, ct)).ToListActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.AgendaWrite)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest body, CancellationToken ct)
        => (await tasks.CreateAsync(body, ct)).ToActionResult(t => Created($"/api/tasks/{t.Id}", t));

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Permissions.AgendaWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest body, CancellationToken ct)
        => (await tasks.UpdateAsync(id, body, ct)).ToActionResult();

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.AgendaWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await tasks.DeleteAsync(id, ct)).ToActionResult();
}
