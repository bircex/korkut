using Korkut.Gateway;
using Korkut.Web;
using Korkut.Web.Auth;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Security;
using Korkut.Domain.Time;
using OpenTelemetry.Trace;

// Korkut.Gateway — the ONLY public entry point (YARP reverse proxy). Terminates the edge
// concerns (auth check, CORS, rate-limit, W3C trace propagation) and routes by path to the
// right host: the agent surface → Korkut.Agent, everything else under /api → Korkut.Api.
// Knowledge and OCR stay internal (never routed here). Downstream services re-validate the JWT
// and apply RLS, so this is defense in depth, not the only gate. See docs/BACKEND_ARCHITECTURE.md.

var builder = WebApplication.CreateBuilder(args);

// Observability: OTel traces/metrics + redacted logs. ASP.NET Core instrumentation for the
// inbound request; HttpClient instrumentation so the proxied hop is a child span and the W3C
// traceparent propagates downstream (one browser request = one trace across every host).
builder.AddKorkutObservability("Korkut.Gateway", tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation());

// Edge foundation shared with the hosts: JSON contract, error mapping, CORS, rate limiting.
builder.Services.AddKorkutWeb(builder.Configuration);

// Access-token auth so the edge can reject anonymous callers on protected (agent) routes.
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

// A plain "require a valid access token" policy for the protected agent routes (the per-permission
// checks stay downstream). Layered on top of the schemes AddKorkutAuthorization registered.
builder.Services.AddAuthorizationBuilder().AddPolicy(GatewayRoutes.AuthenticatedPolicy, policy => policy
    .AddAuthenticationSchemes(AccessTokenAuthHandler.SchemeName)
    .RequireAuthenticatedUser());

// The reverse proxy: routes + clusters from the env-configured upstream URLs.
builder.Services.AddReverseProxy().LoadFromMemory(GatewayRoutes.Routes(), GatewayRoutes.Clusters(builder.Configuration));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Gateway", status = "ok" }));
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReverseProxy();

app.Run();

// Exposed so a WebApplicationFactory<Program> can boot the gateway in integration tests.
public partial class Program;
