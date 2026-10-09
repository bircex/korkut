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
using Korkut.Api.Setup;
using Korkut.Knowledge;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

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

// Auth services (password/hasher/directory/AuthService/TOTP/Google/OIDC) + the
// refresh-token + Google sign-in services the session endpoints use (KDM-012..017).
builder.Services.AddKorkutAuth(builder.Configuration);
builder.Services.AddScoped<Korkut.Security.RefreshTokenService>();
builder.Services.AddScoped<Korkut.Api.Auth.SessionManager>();

// Google OIDC: typed HttpClient token exchanger + JWKS id-token validator + the flow.
builder.Services.AddGoogleOidcRuntime();

// Self-serve signup ticket (short-lived, own audience) — proof of a verified Google email.
builder.Services.AddSingleton(sp => new SignupTicketService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));

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

// Access-token auth + deny-by-default permission policies (KDM-018/019).
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
// Platform-admin console: cookie session, admin TOTP store, and the admin Google flow
// (same client, separate pre-registered redirect URI).
builder.Services.TryAddSingleton<Korkut.Core.IEnvironment, Korkut.Core.SystemEnvironment>();
builder.Services.AddSingleton<Korkut.Api.Auth.AdminSession>();
builder.Services.AddScoped<Korkut.Security.AdminTwoFactorService>();
builder.Services.AddHttpClient<Korkut.Api.Auth.AdminGoogleFlow>();
builder.Services.AddKorkutAuthorization();

// Bound request body size (tightened per-endpoint for uploads in KDM-026). Chat uploads carry a
// whole document's text, so the ceiling is the agent's 20 MB rather than the API's old 10.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024);

// Health checks (liveness/readiness split, non-leaking responses).
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapKorkutHealthChecks();
app.UseKorkutSpa();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this
// assembly's entry point.
public partial class Program;
