using Korkut.Contract.Organization;
using Korkut.Organization;
using Korkut.Organization.Entities;
using Korkut.Api.Auth;
using Korkut.Security;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Korkut.Api.Controllers;

/// <summary>
/// Google OIDC sign-in. <c>start</c> redirects to Google's consent screen (503 when the
/// deployment has no Google credentials); <c>callback</c> verifies state and the ID token,
/// then either issues the cookie session, gates on TOTP, or lands a verified-but-unknown
/// visitor on the signup page. Every ending is a redirect the browser can follow, and every
/// rejection looks the same from outside: /giris?error=…
/// </summary>
[ApiController]
[Route("api/auth/google")]
[AllowAnonymous]
public sealed class GoogleAuthController(
    IGoogleSignInService google, SessionManager session, SignupTicketService signupTickets) : ControllerBase
{
    [HttpGet("start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        if (!google.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("Google girişi yapılandırılmamış."));
        }

        return Redirect(await google.BeginAsync(ct));
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state, [FromQuery] string? code, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
        {
            return SeeOther("/giris?error=verify");
        }

        var result = await google.CompleteAsync(state, code, ct);
        switch (result.Status)
        {
            case GoogleSignInStatus.Rejected:
                return SeeOther($"/giris?error={MapError(result.Reason)}");

            case GoogleSignInStatus.SignupEligible:
                // Verified e-mail, no account yet → stash a signup ticket and land on /kayit.
                session.SetSignup(Response, signupTickets.Mint(new SignupTicket(result.Email!, result.GoogleSub!)));
                return SeeOther("/kayit");

            case GoogleSignInStatus.TwoFactorRequired:
                var pending = result.User!;
                session.SetPending(Response, pending.OfficeId, pending.UserId, pending.Role, pending.SecurityStamp);
                return SeeOther("/");

            default:
                var user = result.User!;
                await session.IssueAsync(Response, user.OfficeId, user.UserId, user.Role, user.SecurityStamp, ct);
                return SeeOther("/");
        }
    }

    /// <summary>303 See Other: the browser follows with a GET to the SPA.</summary>
    private StatusCodeResult SeeOther(string location)
    {
        Response.Headers.Location = location;
        return StatusCode(StatusCodes.Status303SeeOther);
    }

    /// <summary>
    /// Reasons are collapsed on the way out: the login page only needs to know what to tell
    /// the visitor, not which internal check failed.
    /// </summary>
    private static string MapError(string? reason) => reason switch
    {
        "no_office" => "no_office",
        "office_disabled" => "pending",
        "invite" => "invite",
        _ => "verify",
    };
}
