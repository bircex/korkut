using Korkut.Contract;
using Korkut.Contract.Organization;
using Korkut.Api.Auth;
using Korkut.Security;
using Korkut.Web;
using Korkut.Web.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Controllers;

/// <summary>
/// Self-serve onboarding. After a Google-verified sign-in with no existing account, the
/// callback leaves a short-lived signup ticket cookie and lands the browser on /kayit.
/// Both routes are anonymous but gated by that signed ticket — the identity comes from it,
/// never from the request body — and neither issues a session: the office is created
/// disabled and waits for a platform admin.
/// </summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public sealed class OnboardingController(
    IOnboardingService onboarding, SessionManager session, SignupTicketService tickets) : ControllerBase
{
    [HttpGet("signup/pending")]
    public async Task<IActionResult> Pending()
    {
        var ticket = await ReadTicketAsync() ?? throw ApiException.Unauthorized("Bekleyen bir kayıt bulunamadı.");
        return Ok(new SignupPendingResponse(ticket.Email));
    }

    /// <summary>Local e-mail + password registration (no Google). Creates a disabled office; the
    /// owner can sign in only after a platform admin enables it. Rate-limited like the other
    /// sensitive endpoints; the identity here IS the supplied e-mail + password.</summary>
    [HttpPost("auth/register")]
    [EnableRateLimiting(WebSetup.SensitivePolicy)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct)
        => (await onboarding.RegisterAsync(body, ct)).ToActionResult();

    [HttpPost("demo-request")]
    public async Task<IActionResult> RequestDemo([FromBody] DemoRequestRequest body, CancellationToken ct)
    {
        var ticket = await ReadTicketAsync()
            ?? throw ApiException.Unauthorized("Google ile doğrulanmış bir kayıt talebi gerekli.");

        var result = await onboarding.RequestOfficeAsync(ticket.Email, ticket.GoogleSub, body, ct);
        // Consume the ticket only on success; the office stays disabled until a platform admin approves it.
        if (result.IsSuccess)
        {
            session.ClearSignup(Response);
        }

        return result.ToActionResult();
    }

    private async Task<SignupTicket?> ReadTicketAsync()
    {
        var raw = session.ReadSignup(Request);
        return string.IsNullOrEmpty(raw) ? null : await tickets.ValidateAsync(raw);
    }
}
