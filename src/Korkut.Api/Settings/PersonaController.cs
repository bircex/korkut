using Korkut.Application.Security;
using Korkut.Application.Settings;
using Korkut.Application.Settings.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Settings;

/// <summary>
/// The office-wide AI persona. Everyone working the office's matters may read it; changing
/// it requires office.settings, and the response says which of the two the caller is.
/// </summary>
[ApiController]
[Route("api/persona")]
public sealed class PersonaController(IPersonaService persona) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await persona.GetAsync(ct));

    [HttpPut]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Save([FromBody] SavePersonaRequest body, CancellationToken ct)
        => Ok(await persona.SaveAsync(body, ct));
}
