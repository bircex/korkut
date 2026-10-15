using Korkut.Contract;
using Korkut.Contract.Calendar;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Calendar.Controllers;

/// <summary>
/// The office calendar. <c>GET /events</c> is a UNIFIED read view — the office's agenda records
/// (görev/hatırlatma/randevu, owned by Tasks) plus its real hearings and deadlines, all read-only
/// here. Editable records are created/patched/deleted through <c>/api/tasks</c>. <c>GET /upcoming</c>
/// is the agenda.
/// </summary>
[ApiController]
[Route("api/calendar")]
public sealed class CalendarController(ICalendarService calendar) : ControllerBase
{
    [HttpGet("events")]
    [Authorize(Policy = Permissions.AgendaRead)]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end,
        [FromQuery] string? category, [FromQuery] Guid? matterId, CancellationToken ct)
        => (await calendar.ListEntriesAsync(start, end, category, matterId, ct)).ToListActionResult();

    [HttpGet("upcoming")]
    [Authorize(Policy = Permissions.AgendaRead)]
    public async Task<IActionResult> Upcoming([FromQuery] int? days, CancellationToken ct)
        => (await calendar.AgendaAsync(days ?? 14, ct)).ToActionResult();
}
