using Korkut.Infrastructure.Observability;
using OpenTelemetry.Trace;

// Korkut.Agent — the stateless AI host. Owns every agent operation (sessions, chat,
// streaming, upload, document drafting); scales independently of the CRUD API. See
// docs/BACKEND_ARCHITECTURE.md. Shares Application/Infrastructure with Korkut.Api and
// connects to the SAME Postgres (RLS, korkut_app role); it never migrates on start.
//
// Skeleton (phase 3a): observability + health + shared-library wiring. The agent
// endpoints move here from Korkut.Api behind ISessionRepository/IChatService in the
// follow-up PR, along with the split of the agent integration tests onto this host.

var builder = WebApplication.CreateBuilder(args);

builder.AddKorkutObservability("Korkut.Agent", tracing => tracing.AddAspNetCoreInstrumentation());
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Agent", status = "ok" }));
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed so a WebApplicationFactory<Program> can boot the agent host in integration tests.
public partial class Program;
