using Korkut.Web.Auth;
using Korkut.Api.Auth;
using Korkut.Api.Endpoints;
using Korkut.Api.Observability;
using Korkut.Web;
using Korkut.Web.Controllers;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Storage;
using Korkut.Infrastructure.Security;
using Korkut.Domain.Time;
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
builder.Services.AddScoped<Korkut.Infrastructure.Security.RefreshTokenService>();
builder.Services.AddScoped<Korkut.Api.Auth.SessionManager>();
builder.Services.AddScoped<Korkut.Infrastructure.Persistence.EfResetTokenResolver>();
builder.Services.AddScoped<Korkut.Infrastructure.Persistence.EfOfficeProvisioner>();

// Google OIDC: typed HttpClient token exchanger + JWKS id-token validator + the flow.
builder.Services.AddGoogleOidcRuntime();

// Self-serve signup ticket (short-lived, own audience) — proof of a verified Google email.
builder.Services.AddSingleton(sp => new SignupTicketService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));

// Object storage (MinIO) + the secure-document pipeline services (KDM-025..028).
builder.Services.AddKorkutStorage(builder.Configuration);

// Access-token auth + deny-by-default permission policies (KDM-018/019).
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

// Bound request body size to a sane default (tightened per-endpoint for uploads in KDM-026).
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

// Health checks (liveness/readiness split, non-leaking responses).
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Api", status = "ok" }));
app.MapControllers();
app.MapFoundationEndpoints();
app.MapAuthEndpoints();
app.MapDocumentEndpoints();
app.MapReportsEndpoints();
app.MapOnboardingEndpoints();
app.MapExportEndpoints();
app.MapCoverageEndpoints();
app.MapGoogleAuthEndpoints();
app.MapKorkutHealthChecks();

app.Run();

// Exposed so the integration test host (WebApplicationFactory) can reference this
// assembly's entry point.
public partial class Program;
