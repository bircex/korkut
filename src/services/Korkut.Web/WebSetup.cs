using System.Threading.RateLimiting;
using Asp.Versioning;
using Korkut.Web.Errors;
using Korkut.Web.Json;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Web;

/// <summary>
/// Central composition of the API foundation: JSON contract, API versioning,
/// ProblemDetails/exception handling, CORS, request limits and OpenAPI. Keeping
/// this in one place means every endpoint inherits the same, contract-aligned
/// behavior (KDM-006).
/// </summary>
public static class WebSetup
{
    public const string DefaultCorsPolicy = "korkut-default";

    /// <summary>Rate-limit policy for sensitive endpoints (login/reset/2FA/upload/admin).</summary>
    public const string SensitivePolicy = "sensitive";

    public static IServiceCollection AddKorkutWeb(this IServiceCollection services, IConfiguration configuration)
    {
        // JSON: snake_case + string enums + nulls preserved (Python parity).
        services.ConfigureHttpJsonOptions(o => KorkutJson.Apply(o.SerializerOptions));

        // API versioning via URL segment (/api/v1/...), reported in headers.
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        // Centralized exception → {detail} mapping and a fallback ProblemDetails.
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();

        // CORS: explicit allowlist from configuration (no wildcard with credentials).
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(DefaultCorsPolicy, policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
        }));

        // OpenAPI document generation (served at /openapi/v1.json).
        services.AddOpenApi();

        // Rate limiting: sensitive endpoints are throttled per client IP; over-limit
        // returns 429 (abuse telemetry is the OTel request metrics from KDM-007).
        // Strict by default; overridable via KORKUT_SENSITIVE_RATE_LIMIT so integration
        // tests (which share one host and one client IP) aren't throttled.
        var sensitiveLimit = int.TryParse(configuration["KORKUT_SENSITIVE_RATE_LIMIT"], out var n) ? n : 5;
        var clientIpHeader = configuration[ClientAddress.HeaderSetting];
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Partition by the REAL client, not the socket peer: behind a reverse proxy
            // RemoteIpAddress is always the proxy's address, which would put the whole world in
            // ONE bucket and let the login+2FA flow alone (~5 requests) starve everyone. The
            // proxy's client header is trusted only when the operator names it; see ClientAddress.
            options.AddPolicy(SensitivePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientAddress.Resolve(context, clientIpHeader),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = sensitiveLimit,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    public static WebApplication UseKorkutWeb(this WebApplication app)
    {
        app.UseMiddleware<Korkut.Web.Middleware.SecurityHeadersMiddleware>(app.Environment.IsProduction());
        app.UseExceptionHandler();
        app.UseMiddleware<Korkut.Web.Middleware.CorrelationIdMiddleware>();
        app.UseCors(DefaultCorsPolicy);
        app.UseRateLimiter();
        // The API description is public anyway (the code is open source).
        app.MapOpenApi().AllowAnonymous();
        return app;
    }
}
