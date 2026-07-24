using Korkut.Application.Offices;
using Korkut.Web;
using Korkut.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>
/// Platform-admin office management. A SEPARATE audience — office tokens are rejected by
/// the admin scheme. Cross-tenant by design (offices are the tenant root, not RLS-forced),
/// so nothing here runs in a tenant scope. Every route is rate-limited.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(AuthenticationSchemes = AdminAuthHandler.SchemeName)]
[EnableRateLimiting(WebSetup.SensitivePolicy)]
public sealed class AdminOfficesController(IOfficeAdminService offices) : ControllerBase
{
    [HttpGet("offices")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await offices.ListAsync(ct));

    [HttpPost("offices/{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
    {
        await offices.SetDisabledAsync(id, disabled: false, ct);
        return NoContent();
    }

    [HttpPost("offices/{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        await offices.SetDisabledAsync(id, disabled: true, ct);
        return NoContent();
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
        => Ok(await offices.StatsAsync(ct));
}
