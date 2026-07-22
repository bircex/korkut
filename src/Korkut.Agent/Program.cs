using Korkut.Agent.Endpoints;
using Korkut.Agent.Infrastructure;
using Korkut.Agent.Setup;
using Korkut.Web;
using Korkut.Web.Auth;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using OpenTelemetry.Trace;

// Korkut.Agent — the stateless AI host. Owns every agent operation (sessions, chat,
// streaming, upload, document drafting); scales independently of the CRUD API. See
// docs/BACKEND_ARCHITECTURE.md. It spans TWO stores: its own Agent DB (conversations +
// drafted documents, schema korkut_agent) and the operational DB (office persona / style /
// memory the assistant composes into its prompts) — sharing the SAME Application/
// Infrastructure/Web libraries as Korkut.Api. It never migrates on start; only
// Korkut.Migrator applies schema.

var builder = WebApplication.CreateBuilder(args);

// Observability: OTel traces/metrics + redacted logs, plus ASP.NET Core request instrumentation.
builder.AddKorkutObservability("Korkut.Agent", tracing => tracing.AddAspNetCoreInstrumentation());

// Web foundation shared with the API: JSON contract, versioning, error mapping, CORS, OpenAPI.
builder.Services.AddKorkutWeb(builder.Configuration);

// The operational store the assistant reads from (persona / style / memory + the user-name
// lookup) over the least-privileged app-role connection (KORKUT_DB_URL). Only the DbContext
// is wired here; the specific operational services the agent uses are added below — the full
// operational domain (finance, calendar, …) belongs to Korkut.Api, not this host.
var opDbUrl = builder.Configuration["KORKUT_DB_URL"]
    ?? throw new InvalidOperationException("KORKUT_DB_URL is required for the Agent host (the operational app-role connection the assistant reads office settings from).");
builder.Services.AddKorkutPersistence(opDbUrl);

// The Agent DB (conversations + drafted documents), schema korkut_agent, own RLS + migration
// chain, over the least-privileged app-role connection. Applied only by Korkut.Migrator.
var agentDbUrl = builder.Configuration["KORKUT_AGENT_DB_URL"]
    ?? throw new InvalidOperationException("KORKUT_AGENT_DB_URL is required for the Agent host (the Agent DB app-role connection).");
builder.Services.AddKorkutAgentPersistence(agentDbUrl);

// Shared AI runtime (env/config, PII redactor, "ai" HttpClient, embedder) + the Agent's own chat
// runtime (routing, providers, IChatModel). Providers register only when their key is present; with
// none configured a disabled model reports 503 and the host still boots.
builder.Services.AddKorkutAi();
builder.Services.AddKorkutChat();

// The Agent host's own services: assistant stores → Clean services, AI orchestration
// (chat/drafting), upload text extraction, operational user-name lookup.
builder.Services.AddKorkutAgentServices();

// Clock + access-token auth + deny-by-default permission policies (same scheme as the API,
// so a browser cookie / Bearer token minted by the API authenticates here too).
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

// Bound request body size to a sane default; the agent accepts document attachments on upload.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Agent", status = "ok" }));
app.MapSessionEndpoints();
app.MapDraftDocumentEndpoints();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed so a WebApplicationFactory<Program> can boot the agent host in integration tests.
public partial class Program;
