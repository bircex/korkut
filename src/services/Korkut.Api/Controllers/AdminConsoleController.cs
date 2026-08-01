using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Web;
using Korkut.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

public sealed record AssignModelRequest(string? ModelKey, bool Enabled);

/// <summary>
/// The admin console's data surface under /admin/api — offices, drill-down, stats, and the
/// read-only model catalog. Guarded by the ADMIN scheme (separate audience; cookie or Bearer),
/// cross-tenant by design, rate-limited like every admin route. Enable/disable reuse the
/// existing <see cref="IOfficeAdminService"/>; everything else is <see cref="IAdminConsoleService"/>.
/// </summary>
[ApiController]
[Route("admin/api")]
[Authorize(AuthenticationSchemes = AdminAuthHandler.SchemeName)]
[EnableRateLimiting(WebSetup.SensitivePolicy)]
public sealed class AdminConsoleController(
    IAdminConsoleService console, IOfficeAdminService offices) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
        => (await console.StatsAsync(ct)).ToActionResult();

    [HttpGet("offices")]
    public async Task<IActionResult> Offices(CancellationToken ct)
        => (await console.OfficesAsync(ct)).ToActionResult();

    [HttpGet("offices/{id:guid}")]
    public async Task<IActionResult> OfficeDetail(Guid id, CancellationToken ct)
        => (await console.OfficeDetailAsync(id, ct)).ToActionResult();

    [HttpPost("offices/{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
        => (await offices.SetDisabledAsync(id, disabled: false, ct)).ToActionResult();

    [HttpPost("offices/{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
        => (await offices.SetDisabledAsync(id, disabled: true, ct)).ToActionResult();

    [HttpPut("offices/{id:guid}/model")]
    public async Task<IActionResult> AssignModel(Guid id, [FromBody] AssignModelRequest body, CancellationToken ct)
        => (await console.AssignModelAsync(id, body.ModelKey, body.Enabled, ct)).ToActionResult();

    [HttpGet("models")]
    public async Task<IActionResult> Models(CancellationToken ct)
        => (await console.ModelsAsync(ct)).ToActionResult();
}
