using Korkut.Api.Auth;
using Korkut.Api.Endpoints;
using Korkut.Api.Observability;
using Korkut.Api.Setup;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Observability: OTel traces/metrics + redacted logs, shared with the Worker;
// the API adds ASP.NET Core request instrumentation (KDM-007).
builder.AddKorkutObservability("Korkut.Api", tracing => tracing.AddAspNetCoreInstrumentation());

// API foundation: JSON contract, versioning, error mapping, CORS, OpenAPI (KDM-006).
builder.Services.AddKorkutApi(builder.Configuration);

// Access-token auth + deny-by-default permission policies (KDM-018/019).
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

// Bound request body size to a sane default (tightened per-endpoint for uploads in KDM-026).
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

// Health checks (liveness/readiness split, non-leaking responses).
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutApi();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Api", status = "ok" }));
app.MapFoundationEndpoints();
app.MapKorkutHealthChecks();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this
// assembly's entry point.
public partial class Program;
