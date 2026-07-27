using Korkut.Contract.Calendar;
using Korkut.Core;
using Korkut.Web.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Calendar.Controllers;

/// <summary>
/// The office calendar. <c>GET /events</c> is a UNIFIED read view — editable events plus the
/// office's real hearings and deadlines, read-only. Only event rows can be created/patched/deleted;
/// a hearing or deadline id resolves to 404 here. <c>GET /upcoming</c> is the agenda.
/// </summary>
[ApiController]
[Route("api/calendar")]
public sealed class CalendarController(ICalendarService calendar) : ControllerBase
{
    [HttpGet("events")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end,
        [FromQuery] string? category, [FromQuery] Guid? matterId, CancellationToken ct)
        => (await calendar.ListEntriesAsync(start, end, category, matterId, ct)).ToActionResult();

    [HttpPost("events")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateCalendarEventRequest body, CancellationToken ct)
        => (await calendar.CreateAsync(body, ct)).ToActionResult(c => Created($"/api/calendar/events/{c.Id}", c));

    [HttpPatch("events/{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCalendarEventRequest body, CancellationToken ct)
        => (await calendar.UpdateAsync(id, body, ct)).ToActionResult();

    [HttpDelete("events/{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => (await calendar.DeleteAsync(id, ct)).ToActionResult();

    [HttpGet("upcoming")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Upcoming([FromQuery] int? days, CancellationToken ct)
        => (await calendar.AgendaAsync(days ?? 14, ct)).ToActionResult();
}
