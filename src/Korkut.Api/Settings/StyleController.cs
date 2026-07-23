using Korkut.Application.Security;
using Korkut.Application.Settings;
using Korkut.Application.Settings.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Settings;

/// <summary>
/// Drafting style in two layers. A user edits their own layer with nothing more than
/// matters.read — the write is scoped to their own id, never a requested one — while the
/// office layer everyone inherits needs office.settings.
/// </summary>
[ApiController]
[Route("api/style")]
public sealed class StyleController(IStyleService style) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await style.GetAsync(ct));

    [HttpPut("user")]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> SaveUser([FromBody] SaveStyleRequest body, CancellationToken ct)
        => Ok(await style.SaveUserAsync(body, ct));

    [HttpPut("office")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveOffice([FromBody] SaveStyleRequest body, CancellationToken ct)
        => Ok(await style.SaveOfficeAsync(body, ct));
}
