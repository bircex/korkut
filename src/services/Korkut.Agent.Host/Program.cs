using Korkut.Agent.Host.Setup;
using Korkut.Core;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Persistence;
using Korkut.Security;
using Korkut.Documents.Storage;
using Korkut.Web;
using Korkut.Web.Auth;
using Korkut.Web.Controllers;
using OpenTelemetry.Trace;

// Korkut.Agent — the assistant host. Composes the operational stack (the tools' domain services,
// office Settings, the conversation store) + the Agent DB (drafts + llm-calls) + the agent layers
// (Model -> Brain -> Chat/Drafting) + the tool registry, and serves chat / SSE / upload / drafting.
var builder = WebApplication.CreateBuilder(args);

builder.AddKorkutObservability("Korkut.Agent", tracing => tracing.AddAspNetCoreInstrumentation());

builder.Services.AddKorkutWeb(builder.Configuration);
builder.Services.AddKorkutControllers();

// Operational stack (least-privileged app role): the tools' Contract services, Settings repos,
// and the conversation store all live in the operational DB.
builder.Services.AddKorkutDomain(builder.Configuration);
builder.Services.AddKorkutStorage(builder.Configuration);

// The Agent DB (drafts + versions + the llm_calls ledger). Falls back to the operational
// connection when co-located; never migrated on startup.
var agentDbUrl = builder.Configuration["KORKUT_AGENT_DB_URL"]
    ?? builder.Configuration["KORKUT_DB_URL"]
    ?? throw new InvalidOperationException("KORKUT_AGENT_DB_URL or KORKUT_DB_URL is required for the Agent host.");
builder.Services.AddKorkutAgentPersistence(agentDbUrl);

// The shared AI runtime (embedder + PII redactor + the 'ai' HttpClient) and the agent layers.
builder.Services.AddKorkutAi();
builder.Services.AddKorkutAgentHost();

builder.Services.AddKorkutAuth(builder.Configuration);
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

// Uploaded document text can be large; allow a generous request body for /upload.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Agent", status = "ok" }));
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
