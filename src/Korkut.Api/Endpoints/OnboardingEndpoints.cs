using System.ComponentModel.DataAnnotations;
using Korkut.Api.Auth;
using Korkut.Api.Errors;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Security;

namespace Korkut.Api.Endpoints;

public sealed record SignupPendingResponse(string Email);

public sealed record DemoRequestBody
{
    [Required, MaxLength(200)]
    public string? OfficeName { get; init; }

    /// <summary>The requester's name (captured for the record; optional).</summary>
    [MaxLength(200)]
    public string? FullName { get; init; }
}

public sealed record DemoRequestResponse(bool Pending);

/// <summary>
/// Self-serve onboarding. After a Google-verified sign-in with no existing account, the
/// callback drops a short-lived signup ticket cookie and lands the browser on /kayit.
/// <c>signup/pending</c> reports the pending email; <c>demo-request</c> redeems the
/// ticket to provision a DISABLED office (owner linked to the Google subject) that a
/// platform-admin later enables. No session is issued here. Both are anonymous but
/// gated by the signed signup ticket — not by observed request content.
/// </summary>
public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/signup/pending", async (HttpContext http, SessionManager session, SignupTicketService tickets) =>
            {
                var raw = session.ReadSignup(http.Request);
                var ticket = string.IsNullOrEmpty(raw) ? null : await tickets.ValidateAsync(raw);
                return ticket is null
                    ? throw ApiException.Unauthorized("Bekleyen bir kayıt bulunamadı.")
                    : Results.Ok(new SignupPendingResponse(ticket.Email));
            })
            .AllowAnonymous();

        app.MapPost("/api/demo-request", async (
            DemoRequestBody body, HttpContext http, SessionManager session, SignupTicketService tickets,
            KorkutDbContext db, EfOfficeProvisioner provisioner, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var raw = session.ReadSignup(http.Request);
                var ticket = string.IsNullOrEmpty(raw) ? null : await tickets.ValidateAsync(raw);
                if (ticket is null)
                {
                    throw ApiException.Unauthorized("Google ile doğrulanmış bir kayıt talebi gerekli.");
                }

                var provisioned = await provisioner.ProvisionAsync(ticket.Email, ticket.GoogleSub, body.OfficeName!.Trim(), ct);
                if (provisioned is null)
                {
                    // Email/subject already registered — nothing to create.
                    throw ApiException.Conflict("Bu hesap için zaten bir büro mevcut.");
                }

                // Consume the ticket; the office stays disabled until a platform-admin approves it.
                session.ClearSignup(http.Response);
                return Results.Ok(new DemoRequestResponse(true));
            })
            .AllowAnonymous();

        return app;
    }
}
