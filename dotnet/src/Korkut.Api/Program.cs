using Korkut.Api.Endpoints;
using Korkut.Api.Observability;
using Korkut.Api.Setup;
using Korkut.Infrastructure.Observability;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Observability: OTel traces/metrics + redacted logs, shared with the Worker;
// the API adds ASP.NET Core request instrumentation (KDM-007).
builder.AddKorkutObservability("Korkut.Api", tracing => tracing.AddAspNetCoreInstrumentation());

// API foundation: JSON contract, versioning, error mapping, CORS, OpenAPI (KDM-006).
builder.Services.AddKorkutApi(builder.Configuration);

// Bound request body size to a sane default (tightened per-endpoint for uploads in KDM-026).
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

// Health checks (liveness/readiness split, non-leaking responses).
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutApi();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Api", status = "ok" }));
app.MapFoundationEndpoints();
app.MapKorkutHealthChecks();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this
// assembly's entry point.
public partial class Program;
