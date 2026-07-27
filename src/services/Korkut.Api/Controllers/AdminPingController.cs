using Korkut.Web;
using Korkut.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>Liveness probe for the platform-admin audience: an office token must NOT open this.</summary>
public sealed record AdminPingResponse(bool Admin);

/// <summary>
/// Proves the admin audience is genuinely separate: a valid office token is rejected here
/// because the admin scheme, not the office one, guards it. Rate-limited like every other
/// admin route.
/// </summary>
[ApiController]
[Route("api/admin/ping")]
[Authorize(AuthenticationSchemes = AdminAuthHandler.SchemeName)]
[EnableRateLimiting(WebSetup.SensitivePolicy)]
public sealed class AdminPingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok(new AdminPingResponse(true));
}
