using System.Threading.RateLimiting;
using Asp.Versioning;
using Korkut.Api.Errors;
using Korkut.Api.Json;
using Microsoft.AspNetCore.RateLimiting;

namespace Korkut.Api.Setup;

/// <summary>
/// Central composition of the API foundation: JSON contract, API versioning,
/// ProblemDetails/exception handling, CORS, request limits and OpenAPI. Keeping
/// this in one place means every endpoint inherits the same, contract-aligned
/// behavior (KDM-006).
/// </summary>
public static class ApiSetup
{
    public const string DefaultCorsPolicy = "korkut-default";

    /// <summary>Rate-limit policy for sensitive endpoints (login/reset/2FA/upload/admin).</summary>
    public const string SensitivePolicy = "sensitive";

    public static IServiceCollection AddKorkutApi(this IServiceCollection services, IConfiguration configuration)
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
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(SensitivePolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    public static WebApplication UseKorkutApi(this WebApplication app)
    {
        app.UseMiddleware<Korkut.Api.Middleware.SecurityHeadersMiddleware>(app.Environment.IsProduction());
        app.UseExceptionHandler();
        app.UseMiddleware<Korkut.Api.Middleware.CorrelationIdMiddleware>();
        app.UseCors(DefaultCorsPolicy);
        app.UseRateLimiter();
        app.MapOpenApi();
        return app;
    }
}
