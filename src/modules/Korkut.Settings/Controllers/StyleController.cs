using Korkut.Contract;
using Korkut.Contract.Settings;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Settings.Controllers;

/// <summary>
/// Drafting style in two layers. A user edits their own layer with matters.read (the write is scoped
/// to their own id); the office layer everyone inherits needs office.settings.
/// </summary>
[ApiController]
[Route("api/style")]
public sealed class StyleController(IStyleService style) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ChatUse)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await style.GetAsync(ct)).ToActionResult();

    [HttpPut("user")]
    [Authorize(Policy = Permissions.ChatUse)]
    public async Task<IActionResult> SaveUser([FromBody] SaveStyleRequest body, CancellationToken ct)
        => (await style.SaveUserAsync(body, ct)).ToActionResult();

    [HttpPut("office")]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> SaveOffice([FromBody] SaveStyleRequest body, CancellationToken ct)
        => (await style.SaveOfficeAsync(body, ct)).ToActionResult();
}
