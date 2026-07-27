using Korkut.Api.Contracts;
using Korkut.Api.Auth;
using Korkut.Application.Auth;
using Korkut.Application.Auth.Requests;
using Korkut.Infrastructure.Security;
using Korkut.Web;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>
/// Cookie-based sign-in and session lifecycle: password → (TOTP) → session, refresh with
/// rotation, logout with family revoke, and password recovery. Tokens live only in httpOnly
/// cookies, so the SPA never holds one. Everything here is anonymous by definition and the
/// guessable routes are rate-limited; the decisions all happen in the auth service, which
/// keeps a wrong password and an unknown account indistinguishable.
/// </summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public sealed class AuthController(
    IAuthService auth, SessionManager session, RefreshTokenService refreshTokens) : ControllerBase
{
    [HttpPost("auth/login")]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> Login([FromBody] LoginRequest body, CancellationToken ct)
    {
        var result = await auth.SignInAsync(body, ct);
        if (result.Outcome == AuthOutcome.LockedOut)
        {
            throw ApiException.Locked("Hesap geçici olarak kilitli.");
        }

        if (result.Outcome != AuthOutcome.Success || result.User is null)
        {
            throw InvalidCredentials();
        }

        var user = result.User;
        if (user.TwoFactorEnabled)
        {
            session.SetPending(Response, user.OfficeId, user.UserId, user.Role, user.SecurityStamp);
            return Ok(new LoginResponse(TotpRequired: true));
        }

        await session.IssueAsync(Response, user.OfficeId, user.UserId, user.Role, user.SecurityStamp, ct);
        return Ok(new LoginResponse(TotpRequired: false));
    }

    [HttpPost("auth/2fa")]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> TwoFactor([FromBody] TotpRequest body, CancellationToken ct)
    {
        // The pending cookie is the proof that the password step already passed.
        var pending = await session.ReadPendingAsync(Request);
        if (pending is null)
        {
            throw InvalidCredentials();
        }

        var user = await auth.CompleteTwoFactorAsync(pending.OfficeId, pending.UserId, body, ct)
            ?? throw InvalidCredentials();

        await session.IssueAsync(Response, user.OfficeId, user.UserId, user.Role, user.SecurityStamp, ct);
        return Ok(OkResponse.Done);
    }

    [HttpPost("auth/refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var raw = Request.Cookies[SessionManager.RefreshCookie];
        if (string.IsNullOrEmpty(raw))
        {
            throw InvalidCredentials();
        }

        var outcome = await refreshTokens.RotateAsync(raw, ct);
        if (outcome.Status != RotateStatus.Rotated)
        {
            session.Clear(Response); // reuse/expired/invalid → drop the session
            throw InvalidCredentials();
        }

        // The role and stamp are read fresh, so a role change or a password reset since the
        // last refresh is reflected in (or invalidates) the new access token.
        var user = await auth.FindSessionUserAsync(outcome.OfficeId, outcome.UserId, ct);
        if (user is null)
        {
            session.Clear(Response);
            throw InvalidCredentials();
        }

        session.Rotate(Response, user.OfficeId, user.UserId, user.Role, user.SecurityStamp, outcome.NewRawToken!);
        return Ok(OkResponse.Done);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var raw = Request.Cookies[SessionManager.RefreshCookie];
        if (!string.IsNullOrEmpty(raw))
        {
            // Revoke the whole family so the refresh token cannot be reused after logout.
            var outcome = await refreshTokens.RotateAsync(raw, ct);
            if (outcome.FamilyId != Guid.Empty)
            {
                await refreshTokens.RevokeFamilyAsync(outcome.FamilyId, ct);
            }
        }

        session.Clear(Response);
        return Ok(OkResponse.Done);
    }

    [HttpPost("auth/password/reset-request")]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetRequest body, CancellationToken ct)
    {
        // Always 200: whether the account exists is not the caller's business.
        await auth.RequestPasswordResetAsync(body, ct);
        return Ok(OkResponse.Done);
    }

    [HttpPost("auth/password/reset")]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest body, CancellationToken ct)
    {
        var outcome = await auth.ResetPasswordAsync(body, ct);
        return outcome == ResetOutcome.Success
            ? Ok(OkResponse.Done)
            : throw ApiException.BadRequest("Bağlantı geçersiz veya süresi dolmuş.");
    }

    private static ApiException InvalidCredentials() => ApiException.Unauthorized("E-posta veya parola hatalı.");
}
