using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Web;
using Korkut.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

public sealed record AssignModelRequest(string? ModelKey, bool Enabled);

public sealed record SetDefaultModelRequest(string ModelKey);

/// <summary>
/// The admin console's data surface under /admin/api — offices, drill-down, stats, the model plane
/// (catalog + providers + the platform default), and model-usage reporting. Guarded by the ADMIN
/// scheme (separate audience; cookie or Bearer), cross-tenant by design, rate-limited like every
/// admin route. Enable/disable reuse the existing <see cref="IOfficeAdminService"/>; the model
/// catalog and the default switch are <see cref="IModelCatalogService"/>; usage is
/// <see cref="IUsageReportService"/>; everything else is <see cref="IAdminConsoleService"/>.
/// </summary>
[ApiController]
[Route("admin/api")]
[Authorize(AuthenticationSchemes = AdminAuthHandler.SchemeName)]
[EnableRateLimiting(WebSetup.SensitivePolicy)]
public sealed class AdminConsoleController(
    IAdminConsoleService console, IOfficeAdminService offices, IModelCatalogService catalog,
    IUsageReportService usage) : ControllerBase
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

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
        => (await console.UsersAsync(ct)).ToActionResult();

    [HttpPost("offices/{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct)
        => (await offices.SetDisabledAsync(id, disabled: false, ct)).ToActionResult();

    [HttpPost("offices/{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
        => (await offices.SetDisabledAsync(id, disabled: true, ct)).ToActionResult();

    /// <summary>Pin this office to a catalog model (or clear the pin so it follows the platform
    /// default). Live: the chat router honours it on the office's next turn.</summary>
    [HttpPut("offices/{id:guid}/model")]
    public async Task<IActionResult> AssignModel(Guid id, [FromBody] AssignModelRequest body, CancellationToken ct)
        => (await console.AssignModelAsync(id, body.ModelKey, body.Enabled, ct)).ToActionResult();

    [HttpGet("models")]
    public async Task<IActionResult> Models(CancellationToken ct)
        => (await catalog.ListAsync(ct)).ToActionResult();

    /// <summary>Create or replace a catalog entry. The key in the route is authoritative.</summary>
    [HttpPut("models/{key}")]
    public async Task<IActionResult> UpsertModel(
        string key, [FromBody] AdminModelUpsertRequest body, CancellationToken ct)
        => (await catalog.UpsertAsync(body with { Key = key }, ct)).ToActionResult();

    [HttpDelete("models/{key}")]
    public async Task<IActionResult> DeleteModel(string key, CancellationToken ct)
        => (await catalog.DeleteAsync(key, ct)).ToActionResult();

    /// <summary>The providers this build can route to, and whether each one's API key is present.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken ct)
        => (await catalog.ProvidersAsync(ct)).ToActionResult();

    /// <summary>The system-wide default model — what every office without its own pin uses.</summary>
    [HttpGet("settings/default-model")]
    public async Task<IActionResult> DefaultModel(CancellationToken ct)
        => (await catalog.GetDefaultAsync(ct)).ToActionResult();

    /// <summary>Repoint the one-switch system default. It can only be REPLACED, never cleared — see
    /// IModelCatalogService.SetDefaultAsync.</summary>
    [HttpPut("settings/default-model")]
    public async Task<IActionResult> SetDefaultModel([FromBody] SetDefaultModelRequest body, CancellationToken ct)
        => (await catalog.SetDefaultAsync(body.ModelKey, ct)).ToActionResult();

    /// <summary>One office's token/cost usage, broken down by member, model, and feature.</summary>
    [HttpGet("offices/{id:guid}/usage")]
    public async Task<IActionResult> OfficeUsage(
        Guid id, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var (start, end) = Window(from, to);
        return (await usage.OfficeUsageAsync(id, start, end, ct)).ToActionResult();
    }

    /// <summary>Platform-wide token/cost usage, broken down by office and model.</summary>
    [HttpGet("usage/summary")]
    public async Task<IActionResult> UsageSummary(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var (start, end) = Window(from, to);
        return (await usage.SummaryAsync(start, end, ct)).ToActionResult();
    }

    /// <summary>Unbounded by default — the console's headline figure is lifetime usage; the query
    /// params narrow it. The upper bound is pushed a day out so "now" is always inside the window.</summary>
    private static (DateTimeOffset From, DateTimeOffset To) Window(DateTimeOffset? from, DateTimeOffset? to)
        => (from ?? DateTimeOffset.UnixEpoch, to ?? DateTimeOffset.UtcNow.AddDays(1));
}
