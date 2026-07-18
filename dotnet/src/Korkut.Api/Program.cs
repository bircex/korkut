using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Health checks: liveness (process up) and readiness (dependencies ready). Real
// dependency probes (PostgreSQL, MinIO, Redis) are added with those integrations
// in later KDM issues; the scaffold wires the endpoints and a self check now.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Api", status = "ok" }));

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
