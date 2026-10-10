using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>
/// First-run setup of a fresh install. Anonymous by necessity: there is nobody to sign in as
/// until it has run. It creates the office and its owner once and never again; the app then
/// signs the owner in through the ordinary login. The status is public so the app knows whether
/// to show the setup screen or the sign-in.
/// </summary>
[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public sealed class SetupController(ISetupService setup) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(await setup.StatusAsync(ct));

    [HttpPost]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> Setup([FromBody] SetupRequest body, CancellationToken ct)
        => (await setup.SetupAsync(body, ct)).ToActionResult();
}
