using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Auth;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Api.Endpoints;

public sealed record MeResponse(string Username, string Email, string? FullName, string? Office, string Role);

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public string? Email { get; init; }

    [Required]
    public string? Password { get; init; }
}

public sealed record LoginResponse(bool TotpRequired);

public sealed record TotpRequest
{
    public string? Code { get; init; }

    public string? RecoveryCode { get; init; }
}

public sealed record ResetRequestBody
{
    [Required, EmailAddress]
    public string? Email { get; init; }
}

public sealed record ResetBody
{
    [Required]
    public string? Token { get; init; }

    [Required, MinLength(10, ErrorMessage = "Parola en az 10 karakter olmalıdır.")]
    public string? Password { get; init; }
}

/// <summary>
/// Cookie-based auth/session on the .NET runtime (KDM-030 contract): local login →
/// policy-driven TOTP → session; refresh with rotation; logout with family revoke;
/// and the <c>/api/me</c> probe. Tokens live only in httpOnly cookies. Login resolves
/// the office cross-tenant (the sanctioned BYPASSRLS resolver), then everything runs
/// tenant-scoped so RLS applies.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (ClaimsPrincipal user, KorkutDbContext db, CancellationToken ct) =>
            {
                var me = await user.InTenantScopeAsync(db, async office =>
                {
                    var uid = user.UserId();
                    var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == office && x.Id == uid, ct);
                    if (u is null)
                    {
                        return null;
                    }

                    var officeName = await db.Offices.AsNoTracking().Where(x => x.Id == office).Select(x => x.Name).SingleOrDefaultAsync(ct);
                    return new MeResponse(u.Email, u.Email, null, officeName, u.Role);
                }, ct);
                return me is null ? throw ApiException.Unauthorized("Oturum bulunamadı.") : Results.Ok(me);
            })
            .RequireAuthorization();

        app.MapPost("/api/auth/login", HandleLogin)
            .AllowAnonymous()
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        app.MapPost("/api/auth/2fa", HandleTotp)
            .AllowAnonymous()
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        app.MapPost("/api/auth/refresh", HandleRefresh).AllowAnonymous();

        app.MapPost("/api/logout", HandleLogout).AllowAnonymous();

        app.MapPost("/api/auth/password/reset-request", HandleResetRequest)
            .AllowAnonymous()
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        app.MapPost("/api/auth/password/reset", HandleReset)
            .AllowAnonymous()
            .RequireRateLimiting(Setup.ApiSetup.SensitivePolicy);

        return app;
    }

    private static async Task<IResult> HandleResetRequest(
        ResetRequestBody body, KorkutDbContext db, IGoogleUserDirectory resolver, AuthService auth, CancellationToken ct)
    {
        var invalid = DtoValidation.Validate(body);
        if (invalid is not null)
        {
            return invalid;
        }

        // Resolve the office cross-tenant; a request for an unknown account still
        // returns 200 (never reveal whether the account exists).
        var email = body.Email!.Trim().ToLowerInvariant();
        var candidates = await resolver.FindByEmailAcrossOfficesAsync(email, ct);
        if (candidates.Count == 1)
        {
            var officeId = candidates[0].OfficeId;
            await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
            await auth.RequestPasswordResetAsync(officeId, email, ct);
            await scope.CommitAsync(ct);
        }

        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> HandleReset(
        ResetBody body, KorkutDbContext db, EfResetTokenResolver tokens, ITokenHasher hasher, AuthService auth, CancellationToken ct)
    {
        var invalid = DtoValidation.Validate(body);
        if (invalid is not null)
        {
            return invalid;
        }

        var owner = await tokens.ResolveAsync(hasher.Hash(body.Token!), ct);
        if (owner is null)
        {
            return Results.Json(new { detail = "Bağlantı geçersiz veya süresi dolmuş." }, statusCode: 400);
        }

        await using var scope = await db.BeginTenantScopeAsync(owner.OfficeId, ct);
        var outcome = await auth.ResetPasswordAsync(owner.OfficeId, body.Token!, body.Password!, ct);
        await scope.CommitAsync(ct);
        return outcome == ResetOutcome.Success
            ? Results.Ok(new { ok = true })
            : Results.Json(new { detail = "Bağlantı geçersiz veya süresi dolmuş." }, statusCode: 400);
    }

    private static async Task<IResult> HandleLogin(
        LoginRequest body, HttpContext http, KorkutDbContext db, IGoogleUserDirectory resolver,
        AuthService auth, SessionManager session, CancellationToken ct)
    {
        var invalid = DtoValidation.Validate(body);
        if (invalid is not null)
        {
            return invalid;
        }

        var email = body.Email!.Trim().ToLowerInvariant();
        // Cross-tenant resolve (BYPASSRLS SECURITY DEFINER). A generic 401 for 0-or-many
        // candidates keeps the endpoint enumeration-safe.
        var candidates = await resolver.FindByEmailAcrossOfficesAsync(email, ct);
        if (candidates.Count != 1)
        {
            return Unauthorized();
        }

        var officeId = candidates[0].OfficeId;
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var outcome = await auth.AuthenticateAsync(officeId, email, body.Password!, ct);
        if (outcome != AuthOutcome.Success)
        {
            await scope.CommitAsync(ct);
            return outcome == AuthOutcome.LockedOut
                ? Results.Json(new { detail = "Hesap geçici olarak kilitli." }, statusCode: 423)
                : Unauthorized();
        }

        var u = await db.Users.AsNoTracking().SingleAsync(x => x.OfficeId == officeId && x.Email == email, ct);
        if (u.TwoFactorEnabled)
        {
            session.SetPending(http.Response, officeId, u.Id, u.Role, u.SecurityStamp);
            await scope.CommitAsync(ct);
            return Results.Ok(new LoginResponse(TotpRequired: true));
        }

        await session.IssueAsync(http.Response, officeId, u.Id, u.Role, u.SecurityStamp, ct);
        await scope.CommitAsync(ct);
        return Results.Ok(new LoginResponse(TotpRequired: false));
    }

    private static async Task<IResult> HandleTotp(
        TotpRequest body, HttpContext http, KorkutDbContext db, SessionManager session,
        TwoFactorService totp, CancellationToken ct)
    {
        var pending = await session.ReadPendingAsync(http.Request);
        if (pending is null)
        {
            return Unauthorized();
        }

        var (office, userId) = (pending.OfficeId, pending.UserId);
        await using var scope = await db.BeginTenantScopeAsync(office, ct);

        var ok = !string.IsNullOrWhiteSpace(body.RecoveryCode)
            ? await totp.UseRecoveryCodeAsync(office, userId, body.RecoveryCode!.Trim(), ct)
            : (await totp.ChallengeAsync(office, userId, (body.Code ?? string.Empty).Trim(), ct)) == TwoFactorResult.Success;

        if (!ok)
        {
            await scope.CommitAsync(ct);
            return Unauthorized();
        }

        var u = await db.Users.AsNoTracking().SingleAsync(x => x.OfficeId == office && x.Id == userId, ct);
        await session.IssueAsync(http.Response, office, userId, u.Role, u.SecurityStamp, ct);
        await scope.CommitAsync(ct);
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> HandleRefresh(
        HttpContext http, KorkutDbContext db, RefreshTokenService refresh, SessionManager session, CancellationToken ct)
    {
        var raw = http.Request.Cookies[SessionManager.RefreshCookie];
        if (string.IsNullOrEmpty(raw))
        {
            return Unauthorized();
        }

        var outcome = await refresh.RotateAsync(raw, ct);
        if (outcome.Status != RotateStatus.Rotated)
        {
            session.Clear(http.Response); // reuse/expired/invalid → drop the session
            return Unauthorized();
        }

        // Fetch the current role/stamp (tenant-scoped) to mint the new access token.
        await using var scope = await db.BeginTenantScopeAsync(outcome.OfficeId, ct);
        var u = await db.Users.AsNoTracking().SingleAsync(x => x.OfficeId == outcome.OfficeId && x.Id == outcome.UserId, ct);
        session.Rotate(http.Response, outcome.OfficeId, outcome.UserId, u.Role, u.SecurityStamp, outcome.NewRawToken!);
        await scope.CommitAsync(ct);
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> HandleLogout(
        HttpContext http, KorkutDbContext db, RefreshTokenService refresh, SessionManager session, CancellationToken ct)
    {
        var raw = http.Request.Cookies[SessionManager.RefreshCookie];
        if (!string.IsNullOrEmpty(raw))
        {
            // Revoke the whole family so the refresh token can't be reused after logout.
            var outcome = await refresh.RotateAsync(raw, ct);
            if (outcome.FamilyId != Guid.Empty)
            {
                await refresh.RevokeFamilyAsync(outcome.FamilyId, ct);
            }
        }

        session.Clear(http.Response);
        return Results.Ok(new { ok = true });
    }

    private static IResult Unauthorized() => Results.Json(new { detail = "E-posta veya parola hatalı." }, statusCode: 401);
}
