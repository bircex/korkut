using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Korkut.Api.Observability;

/// <summary>
/// Health endpoint wiring that never discloses sensitive detail. The response is
/// a minimal <c>{ "status": "Healthy" }</c> — no check names, descriptions,
/// exception text, durations or config. Liveness and readiness are separated so
/// an orchestrator can probe "process up" vs "dependencies ready" independently.
/// </summary>
public static class HealthEndpoints
{
    private static readonly JsonSerializerOptions Json = new();

    public static IEndpointRouteBuilder MapKorkutHealthChecks(this IEndpointRouteBuilder app)
    {
        // Liveness: process is up (no dependency checks).
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteMinimalStatus,
        }).AllowAnonymous();

        // Readiness: dependency-tagged checks (none registered until KDM-011+).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteMinimalStatus,
        }).AllowAnonymous();

        return app;
    }

    private static Task WriteMinimalStatus(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        // Only the overall status leaves the process — never entry details/exceptions.
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { status = report.Status.ToString() }, Json));
    }
}
