namespace Korkut.Web.Middleware;

/// <summary>
/// Browser security headers on every response. In the Production profile HSTS is
/// added and its absence is a hard failure — the header policy is mandatory in
/// production (KDM-018). CSP `frame-ancestors 'none'` + `X-Frame-Options: DENY`
/// block clickjacking; `nosniff` and a strict referrer policy round out the
/// baseline. API responses are JSON, so a restrictive default-src is safe.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, bool isProduction)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        h["Cross-Origin-Opener-Policy"] = "same-origin";
        h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        if (isProduction)
        {
            // 1 year, includeSubDomains — only meaningful over HTTPS in production.
            h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await next(context);
    }
}
