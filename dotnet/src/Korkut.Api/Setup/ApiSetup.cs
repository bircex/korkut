using Asp.Versioning;
using Korkut.Api.Errors;
using Korkut.Api.Json;

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

        return services;
    }

    public static WebApplication UseKorkutApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<Korkut.Api.Middleware.CorrelationIdMiddleware>();
        app.UseCors(DefaultCorsPolicy);
        app.MapOpenApi();
        return app;
    }
}
