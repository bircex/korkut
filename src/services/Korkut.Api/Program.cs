using Microsoft.Extensions.DependencyInjection.Extensions;
using Korkut.Web.Auth;
using Korkut.Api.Auth;
using Korkut.Api.Observability;
using Korkut.Web;
using Korkut.Web.Controllers;
using Korkut.Infrastructure.Observability;
using Korkut.Documents.Storage;
using Korkut.Security;
using Korkut.Core;
using Korkut.Agent.Persistence;
using Korkut.Ai;
using Korkut.Api;
using Korkut.Api.Jobs;
using Korkut.Api.Setup;
using Korkut.Knowledge;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// A local install needs no configuration: secrets missing from the environment are generated
// once into the shared secrets volume and reused (see LocalSecrets).
builder.Configuration.AddKorkutLocalSecrets();

// Observability: OTel traces/metrics + redacted logs, shared with the Worker;
// the API adds ASP.NET Core request instrumentation (KDM-007).
builder.AddKorkutObservability("Korkut.Api", tracing => tracing.AddAspNetCoreInstrumentation());

// API foundation: JSON contract, versioning, error mapping, CORS, OpenAPI (KDM-006).
builder.Services.AddKorkutWeb(builder.Configuration);

// Controllers (per-aggregate, clean-architecture slices) on the shared JSON + 422 contract.
builder.Services.AddKorkutControllers();

// Domain persistence + services (app-role DbContext, office-core stores). The real
// endpoints are added per module; DI here makes their services available.
builder.Services.AddKorkutDomain(builder.Configuration);

// Auth services (password/hasher/directory/AuthService/TOTP) + the refresh-token service the
// session endpoints use (KDM-012..017). Sign-in is local only: e-mail (or the admin's user name) and password.
builder.Services.AddKorkutAuth(builder.Configuration);
builder.Services.AddScoped<Korkut.Security.RefreshTokenService>();
builder.Services.AddScoped<Korkut.Api.Auth.SessionManager>();


// Object storage + the secure-document pipeline services (KDM-025..028).
builder.Services.AddKorkutStorage(builder.Configuration);

// The assistant: its own DB (drafts + versions + the llm_calls ledger) and the RAG index, each
// falling back to the operational connection when co-located (one Postgres, separate schemas;
// never migrated on startup), the shared AI runtime, and the model/brain/chat/drafting layers.
var operationalDbUrl = builder.Configuration["KORKUT_DB_URL"];
builder.Services.AddKorkutAgentPersistence(builder.Configuration["KORKUT_AGENT_DB_URL"] ?? operationalDbUrl
    ?? throw new InvalidOperationException("KORKUT_DB_URL is required."));
builder.Services.AddKorkutKnowledge(builder.Configuration["KORKUT_KNOWLEDGE_DB_URL"] ?? operationalDbUrl
    ?? throw new InvalidOperationException("KORKUT_DB_URL is required."));
builder.Services.AddKorkutAi();
builder.Services.AddKorkutAgent();

// The model gateway's management API, behind the owner-only AI settings screen. Without
// KORKUT_LLMR_URL the client has no base address and the screen answers 503.
builder.Services.AddHttpClient(Korkut.Api.Controllers.AiSettingsController.HttpClientName, client =>
{
    var url = builder.Configuration["KORKUT_LLMR_URL"];
    if (!string.IsNullOrWhiteSpace(url))
    {
        client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
    }

    var token = builder.Configuration["KORKUT_LLMR_TOKEN"];
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    // A provider test may make one real model call.
    client.Timeout = TimeSpan.FromSeconds(150);
});

// The installation's office and its admin (korkut-admin), created on first start from the
// password the installer generated. Runs before the app takes requests.
builder.Services.AddHostedService<InstallationAdminBootstrap>();

// Background jobs from the outbox: putting uploaded documents' text into the search index.
builder.Services.AddKorkutBackgroundJobs(builder.Configuration);

// Access-token auth + deny-by-default permission policies (KDM-018/019).
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.TryAddSingleton<Korkut.Core.IEnvironment, Korkut.Core.SystemEnvironment>();
builder.Services.AddKorkutAuthorization();

// Bound request body size (tightened per-endpoint for uploads in KDM-026). Chat uploads carry a
// whole document's text, so the ceiling is the agent's 20 MB rather than the API's old 10.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024);

// Health checks (liveness/readiness split, non-leaking responses).
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseKorkutSpaFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapKorkutHealthChecks();
app.MapKorkutSpa();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this
// assembly's entry point.
public partial class Program;
