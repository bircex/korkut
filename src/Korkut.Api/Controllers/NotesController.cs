using Korkut.Application.Notes;
using Korkut.Application.Notes.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>Communication notes (görüşme notları). Forwards to <see cref="INoteService"/>.</summary>
[ApiController]
[Route("api/notes")]
public sealed class NotesController(INoteService notes) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List([FromQuery] Guid? matterId, CancellationToken ct)
        => Ok(await notes.ListAsync(matterId, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest body, CancellationToken ct)
    {
        var created = await notes.CreateAsync(body, ct);
        return Created($"/api/notes/{created.Id}", created);
    }
}
