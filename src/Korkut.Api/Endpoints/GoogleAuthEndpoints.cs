using Korkut.Api.Auth;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Api.Endpoints;

/// <summary>
/// Google OIDC sign-in (KDM-016 contract): <c>start</c> redirects to Google's consent
/// screen (503 if unconfigured); <c>callback</c> verifies state + the RS256 ID token,
/// then issues the cookie session (or gates on TOTP) and redirects to the SPA — or to
/// <c>/giris?error=…</c> on any rejection. Tokens live only in httpOnly cookies.
/// </summary>
public static class GoogleAuthEndpoints
{
    public static IEndpointRouteBuilder MapGoogleAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/google/start", async (GoogleSignInService google, CancellationToken ct) =>
            {
                try
                {
                    var url = await google.BeginAsync(ct);
                    return Results.Redirect(url);
                }
                catch (InvalidOperationException)
                {
                    return Results.Json(new { detail = "Google girişi yapılandırılmamış." }, statusCode: 503);
                }
            })
            .AllowAnonymous();

        app.MapGet("/api/auth/google/callback", async (
            string? state, string? code, string? error,
            HttpContext http, GoogleSignInService google, SessionManager session, KorkutDbContext db, CancellationToken ct) =>
            {
                if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code))
                {
                    return Redirect("/giris?error=verify");
                }

                var result = await google.CompleteAsync(state, code, ct);
                if (result.Status == GoogleSignInStatus.Rejected)
                {
                    return Redirect($"/giris?error={MapError(result.Reason)}");
                }

                // Fetch the resolved user's role/stamp (tenant-scoped) to mint the session.
                var officeId = result.OfficeId!.Value;
                var userId = result.UserId!.Value;
                await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
                var user = await db.Users.AsNoTracking().SingleAsync(x => x.OfficeId == officeId && x.Id == userId, ct);

                if (result.Status == GoogleSignInStatus.TwoFactorRequired)
                {
                    session.SetPending(http.Response, officeId, userId, user.Role, user.SecurityStamp);
                }
                else
                {
                    await session.IssueAsync(http.Response, officeId, userId, user.Role, user.SecurityStamp, ct);
                }

                await scope.CommitAsync(ct);
                return Redirect("/");
            })
            .AllowAnonymous();

        return app;
    }

    // 303 See Other: the browser follows with a GET to the SPA.
    private static IResult Redirect(string location) => Results.Extensions.SeeOther(location);

    private static string MapError(string? reason) => reason switch
    {
        "no_office" => "no_office",
        "office_disabled" => "pending",
        "invite" => "invite",
        _ => "verify",
    };
}

/// <summary>A 303 See Other result (a GET redirect for the OAuth callback landing).</summary>
internal static class SeeOtherExtensions
{
    public static IResult SeeOther(this IResultExtensions _, string location) => new SeeOtherResult(location);

    private sealed class SeeOtherResult(string location) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
            httpContext.Response.Headers.Location = location;
            return Task.CompletedTask;
        }
    }
}
