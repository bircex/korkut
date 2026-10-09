using Korkut.Gateway;
using Korkut.Web;
using Korkut.Web.Auth;
using Korkut.Infrastructure.Observability;
using Korkut.Security;
using Korkut.Core;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddKorkutObservability("Korkut.Gateway", tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation());

builder.Services.AddKorkutWeb(builder.Configuration);

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

builder.Services.AddAuthorizationBuilder().AddPolicy(GatewayRoutes.AuthenticatedPolicy, policy => policy
    .AddAuthenticationSchemes(AccessTokenAuthHandler.SchemeName)
    .RequireAuthenticatedUser());

builder.Services.AddReverseProxy().LoadFromMemory(GatewayRoutes.Routes(), GatewayRoutes.Clusters(builder.Configuration));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReverseProxy();
app.UseKorkutSpa();

app.Run();

// Exposed so a WebApplicationFactory<Program> can boot the gateway in integration tests.
public partial class Program;
