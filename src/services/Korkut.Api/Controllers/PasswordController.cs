using Korkut.Api.Auth;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Web;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>
/// The signed-in member changing their own password. Changing it ends every other session, so
/// this one is issued again on the spot and the member stays signed in here.
/// </summary>
[ApiController]
[Route("api/me/password")]
[Authorize]
public sealed class PasswordController(IAuthService auth, SessionManager session, ICurrentUser current) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> Change([FromBody] ChangePasswordRequest body, CancellationToken ct)
    {
        var user = await auth.ChangePasswordAsync(current.OfficeId, current.UserId, body, ct)
            ?? throw ApiException.BadRequest("Mevcut parola yanlış.");

        await session.IssueAsync(Response, user.OfficeId, user.UserId, user.Role, user.SecurityStamp, ct);
        return NoContent();
    }
}
