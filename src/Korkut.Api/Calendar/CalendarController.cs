using Korkut.Application.Calendar;
using Korkut.Application.Calendar.Requests;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Calendar;

/// <summary>
/// The office calendar. <c>GET /events</c> is a UNIFIED read view — editable events plus the
/// office's real hearings and deadlines, read-only, so the calendar is complete. Only event
/// rows can be created/patched/deleted; a hearing or deadline id resolves to 404 here.
/// <c>GET /upcoming</c> is the agenda.
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
        => Ok(await calendar.ListEntriesAsync(start, end, category, matterId, ct));

    [HttpPost("events")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Create([FromBody] CreateCalendarEventRequest body, CancellationToken ct)
    {
        var created = await calendar.CreateAsync(body, ct);
        return Created($"/api/calendar/events/{created.Id}", created);
    }

    [HttpPatch("events/{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCalendarEventRequest body, CancellationToken ct)
        => Ok(await calendar.UpdateAsync(id, body, ct));

    [HttpDelete("events/{id:guid}")]
    [Authorize(Policy = Permissions.MattersWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await calendar.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("upcoming")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Upcoming([FromQuery] int? days, CancellationToken ct)
        => Ok(await calendar.AgendaAsync(days ?? 14, ct));
}
