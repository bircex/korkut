using Korkut.Contract.Reports;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Reports.Controllers;

/// <summary>
/// The read-only office dashboard. It surfaces money, so it requires billing.read (both
/// built-in roles hold it).
/// </summary>
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IReportsService reports) : ControllerBase
{
    [HttpGet("overview")]
    [Authorize(Policy = Permissions.BillingRead)]
    public async Task<IActionResult> Overview([FromQuery] int? days, CancellationToken ct)
        => Ok(await reports.OverviewAsync(days, ct));
}
