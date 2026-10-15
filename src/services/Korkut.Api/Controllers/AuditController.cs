using Korkut.Contract.Audit;
using Korkut.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>The office's access log ("Erişim kayıtları"), for those who may read it.</summary>
[ApiController]
[Route("api/audit")]
public sealed class AuditController(IAuditQuery audit) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.AuditRead)]
    public async Task<IActionResult> List([FromQuery] int? limit, [FromQuery] int? offset, [FromQuery] string? action, CancellationToken ct)
        => Ok(await audit.ListAsync(limit ?? 50, offset ?? 0, action, ct));
}
