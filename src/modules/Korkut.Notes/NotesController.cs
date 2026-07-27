using Korkut.Contract.Notes;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Notes;

/// <summary>Communication notes (görüşme notları). Forwards to <see cref="INoteService"/>, maps Result to HTTP.</summary>
[ApiController]
[Route("api/notes")]
public sealed class NotesController(INoteService notes) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => (await notes.ListAsync(matterId, ct)).ToActionResult();

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest body, CancellationToken ct)
        => (await notes.CreateAsync(body, ct)).ToActionResult(n => Created($"/api/notes/{n.Id}", n));
}
