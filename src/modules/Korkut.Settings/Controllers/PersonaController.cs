using Korkut.Contract;
using Korkut.Contract.Settings;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Settings.Controllers;

/// <summary>The office-wide AI persona. Read needs matters.read; change needs office.settings.</summary>
[ApiController]
[Route("api/persona")]
public sealed class PersonaController(IPersonaService persona) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await persona.GetAsync(ct)).ToActionResult();

    [HttpPut]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Save([FromBody] SavePersonaRequest body, CancellationToken ct)
        => (await persona.SaveAsync(body, ct)).ToActionResult();
}
