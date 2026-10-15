using Korkut.Contract;
using Korkut.Contract.Settings;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Settings.Controllers;

/// <summary>The office mail preference. Read needs matters.read; change needs office.settings.</summary>
[ApiController]
[Route("api/mail-preferences")]
public sealed class MailPreferenceController(IMailPreferenceService preferences) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await preferences.GetAsync(ct)).ToActionResult();

    [HttpPut]
    [Authorize(Policy = Permissions.OfficeSettings)]
    public async Task<IActionResult> Save([FromBody] SaveMailPreferenceRequest body, CancellationToken ct)
        => (await preferences.SaveAsync(body, ct)).ToActionResult();
}
