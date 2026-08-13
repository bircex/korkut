using Korkut.Api.Auth;
using Korkut.Security;
using Korkut.Web;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

public sealed record AdminSessionResponse(string State, string? Email = null);

public sealed record AdminTotpRequest(string? Code);

/// <summary>
/// Platform-admin authentication under /admin/api — the server-driven state machine the admin
/// SPA renders (anon / totp_required / bootstrap_required / denied / authed). Google SSO is the
/// only entry, gated by the compiled allowlist, and ALWAYS followed by a Korkut TOTP step; the
/// Google callback never yields a session on its own. Every cookie is httpOnly and path-scoped
/// to /admin/api (see <see cref="AdminSession"/>).
/// </summary>
[ApiController]
[Route("admin/api")]
[AllowAnonymous]
[EnableRateLimiting(WebSetup.SensitivePolicy)]
public sealed class AdminAuthController(
    AdminGoogleFlow google, AdminSession session, AdminTwoFactorService twoFactor) : ControllerBase
{
    [HttpGet("auth/google/start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        if (!google.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse("Google girişi yapılandırılmamış."));
        }

        return Redirect(await google.BeginAsync(ct));
    }

    [HttpGet("auth/google/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? state, [FromQuery] string? code, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
        {
            session.SetDenied(Response);
            return SeeOther("/admin");
        }

        var result = await google.CompleteAsync(state, code, ct);
        switch (result.Status)
        {
            case AdminSignInStatus.TwoFactorRequired:
                session.SetPending(Response, result.Email!, enrolled: true);
                break;
            case AdminSignInStatus.BootstrapRequired:
                session.SetPending(Response, result.Email!, enrolled: false);
                break;
            default:
                // Every rejection looks identical from outside (no allowlist enumeration).
                session.SetDenied(Response);
                break;
        }

        // The admin SPA only mounts AdminConsole (and reads the pending-TOTP cookie we just
        // set) under the /admin path — redirecting to "/" would drop the admin back into the
        // regular member sign-in screen with the cookie never observed.
        return SeeOther("/admin");
    }

    /// <summary>The single source of the SPA's auth state.</summary>
    [HttpGet("session")]
    public async Task<IActionResult> Session()
    {
        if (await session.ReadAsync(Request) is { } authed)
        {
            return Ok(new AdminSessionResponse("authed", authed.SecurityStamp));
        }

        if (await session.ReadPendingAsync(Request) is { } pending)
        {
            return Ok(new AdminSessionResponse(
                pending.Role == AdminSession.PendingTotpRole ? "totp_required" : "bootstrap_required",
                pending.SecurityStamp));
        }

        return Ok(new AdminSessionResponse(session.IsDenied(Request) ? "denied" : "anon"));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
        => await session.ReadAsync(Request) is { } claims
            ? Ok(new { email = claims.SecurityStamp })
            : Unauthorized(new ErrorResponse("Oturum yok."));

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        session.Clear(Response);
        return Ok(new { ok = true });
    }

    /// <summary>Begin (or re-roll) TOTP enrollment — only for a bootstrap-stage pending admin.</summary>
    [HttpPost("2fa/enroll/start")]
    public async Task<IActionResult> EnrollStart(CancellationToken ct)
    {
        if (await session.ReadPendingAsync(Request) is not { Role: AdminSession.PendingBootstrapRole } pending)
        {
            return Unauthorized(new ErrorResponse("Kayıt için önce Google ile doğrulanmalısınız."));
        }

        var (uri, secret) = await twoFactor.BeginEnrollmentAsync(pending.SecurityStamp, ct);
        return Ok(new { provisioning_uri = uri, secret });
    }

    /// <summary>Confirm enrollment with a live code: proves possession, issues the session,
    /// and returns the one-time recovery codes.</summary>
    [HttpPost("2fa/enroll/confirm")]
    public async Task<IActionResult> EnrollConfirm([FromBody] AdminTotpRequest body, CancellationToken ct)
    {
        if (await session.ReadPendingAsync(Request) is not { Role: AdminSession.PendingBootstrapRole } pending)
        {
            return Unauthorized(new ErrorResponse("Kayıt için önce Google ile doğrulanmalısınız."));
        }

        var (result, recovery) = await twoFactor.ConfirmEnrollmentAsync(pending.SecurityStamp, body.Code?.Trim() ?? string.Empty, ct);
        if (result != TwoFactorResult.Success)
        {
            return Unauthorized(new ErrorResponse("Kod doğrulanamadı."));
        }

        session.Issue(Response, pending.SecurityStamp);
        return Ok(new { recovery_codes = recovery });
    }

    /// <summary>The mandatory TOTP challenge after Google — success issues the session.</summary>
    [HttpPost("2fa/challenge")]
    public async Task<IActionResult> Challenge([FromBody] AdminTotpRequest body, CancellationToken ct)
    {
        if (await session.ReadPendingAsync(Request) is not { Role: AdminSession.PendingTotpRole } pending)
        {
            return Unauthorized(new ErrorResponse("Önce Google ile doğrulanmalısınız."));
        }

        var result = await twoFactor.ChallengeAsync(pending.SecurityStamp, body.Code?.Trim() ?? string.Empty, ct);
        if (result != TwoFactorResult.Success)
        {
            return Unauthorized(new ErrorResponse("Kod doğrulanamadı."));
        }

        session.Issue(Response, pending.SecurityStamp);
        return Ok(new AdminSessionResponse("authed", pending.SecurityStamp));
    }

    /// <summary>Single-use recovery code in place of a TOTP code.</summary>
    [HttpPost("2fa/recovery")]
    public async Task<IActionResult> Recovery([FromBody] AdminTotpRequest body, CancellationToken ct)
    {
        if (await session.ReadPendingAsync(Request) is not { Role: AdminSession.PendingTotpRole } pending)
        {
            return Unauthorized(new ErrorResponse("Önce Google ile doğrulanmalısınız."));
        }

        if (!await twoFactor.UseRecoveryCodeAsync(pending.SecurityStamp, body.Code?.Trim() ?? string.Empty, ct))
        {
            return Unauthorized(new ErrorResponse("Kod doğrulanamadı."));
        }

        session.Issue(Response, pending.SecurityStamp);
        return Ok(new AdminSessionResponse("authed", pending.SecurityStamp));
    }

    private StatusCodeResult SeeOther(string location)
    {
        Response.Headers.Location = location;
        return StatusCode(StatusCodes.Status303SeeOther);
    }
}
