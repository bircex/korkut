using Korkut.Agent.Extensions;
using Korkut.Web;
using Korkut.Web.Auth;
using Korkut.Web.Json;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddKorkutObservability("Korkut.Agent", tracing => tracing.AddAspNetCoreInstrumentation());

builder.Services.AddKorkutWeb(builder.Configuration);

var opDbUrl = builder.Configuration["KORKUT_DB_URL"]
              ?? throw new InvalidOperationException("KORKUT_DB_URL is required for the Agent host (the operational app-role connection the assistant reads office settings from).");
builder.Services.AddKorkutPersistence(opDbUrl);

var agentDbUrl = builder.Configuration["KORKUT_AGENT_DB_URL"]
                 ?? throw new InvalidOperationException("KORKUT_AGENT_DB_URL is required for the Agent host (the Agent DB app-role connection).");
builder.Services.AddKorkutAgentPersistence(agentDbUrl);

builder.Services.AddKorkutAi();
builder.Services.AddKorkutChat();

builder.Services.AddKorkutAgentServices();

// Lightweight controllers (Presentation/Controllers) for the assistant + document APIs, using the
// same snake_case JSON contract as the rest of the platform (KorkutJson).
builder.Services.AddControllers().AddJsonOptions(o => KorkutJson.Apply(o.JsonSerializerOptions));

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

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

// Exposed so a WebApplicationFactory<Program> can boot the agent host in integration tests.
public partial class Program;
