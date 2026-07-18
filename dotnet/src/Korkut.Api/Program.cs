using Korkut.Api.Endpoints;
using Korkut.Api.Setup;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// API foundation: JSON contract, versioning, error mapping, CORS, OpenAPI (KDM-006).
builder.Services.AddKorkutApi(builder.Configuration);

// Bound request body size to a sane default (tightened per-endpoint for uploads in KDM-026).
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

// Health checks: liveness (process up) and readiness (dependencies ready). Real
// dependency probes (PostgreSQL, MinIO, Redis) are added with those integrations
// in later KDM issues; the scaffold wires the endpoints and a self check now.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutApi();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Api", status = "ok" }));
app.MapFoundationEndpoints();

// Liveness: is the process running at all (no dependency predicate).
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

// Readiness: are declared dependencies healthy (none registered yet in the scaffold).
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this
// assembly's entry point.
public partial class Program;
