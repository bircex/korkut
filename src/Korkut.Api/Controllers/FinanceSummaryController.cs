using Korkut.Core;
using Korkut.Application.FinanceSummary;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>The office's money roll-up. Forwards to <see cref="IFinanceSummaryService"/>.</summary>
[ApiController]
[Route("api/finance/summary")]
public sealed class FinanceSummaryController(IFinanceSummaryService summary) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.BillingRead)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await summary.GetAsync(ct));
}
