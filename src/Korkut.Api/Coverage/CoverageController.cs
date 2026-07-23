using Korkut.Application.Coverage;
using Korkut.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Coverage;

/// <summary>
/// What the assistant covers: how many document types the catalog has, the per-court
/// petition breakdown, and the source legislation. Reference data, identical for every
/// office; matters.read is enough.
/// </summary>
[ApiController]
[Route("api/coverage")]
public sealed class CoverageController(ICoverageService coverage) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.MattersRead)]
    public IActionResult Get() => Ok(coverage.Get());
}
