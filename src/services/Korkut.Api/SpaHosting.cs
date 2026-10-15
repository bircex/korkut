using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Korkut.Api;

/// <summary>
/// Serves the built React app (ui/, copied into wwwroot by the image) from the same host as the
/// API, so the browser talks to one origin and session cookies stay same-origin. Every non-API path no endpoint claims falls back to index.html for client-side
/// routing (/sohbet/12, /ayarlar/...).
/// </summary>
public static class SpaHosting
{
    // SecurityHeadersMiddleware stamps the API policy (default-src 'none') on every response,
    // which would stop the app from loading at all. App files get this one instead. CKEditor
    // writes inline styles, hence 'unsafe-inline' for styles only; scripts stay 'self'.
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; " +
        "object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    private static readonly StaticFileOptions Files = new() { OnPrepareResponse = ctx => PrepareResponse(ctx.Context) };

    /// <summary>The app's files, served before authentication: the sign-in screen is one of them.</summary>
    public static WebApplication UseKorkutSpaFiles(this WebApplication app)
    {
        app.UseStaticFiles(Files);
        return app;
    }

    /// <summary>Client-side routes get the app shell; the app decides what a signed-out visitor sees.</summary>
    public static WebApplication MapKorkutSpa(this WebApplication app)
    {
        app.MapFallbackToFile("index.html", Files).AllowAnonymous();
        // An /api path no controller claims is a missing endpoint, not a page: answer 404 rather
        // than the app shell, or a client calling a wrong URL gets HTML with a 200.
        app.MapFallback("/api/{**rest}", context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }).AllowAnonymous();
        return app;
    }

    private static void PrepareResponse(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;

        // Vite fingerprints everything under /assets, so those never change under a name; the
        // shell and the unhashed files in public/ must be revalidated or a deploy goes unseen.
        headers[HeaderNames.CacheControl] = context.Request.Path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    }
}
