using Korkut.Application.Documents;
using Korkut.Ocr.Endpoints;
using Korkut.Web;
using Korkut.Web.Auth;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

// Korkut.Ocr — the heavy, rare, on-demand OCR host. One job: turn a scanned image / text-less
// PDF into text. Isolated because it is rare + heavy + native (Tesseract): the fat image + memory
// stay off the always-on hosts, scaled small. Internal-only (the gateway keeps it in-cluster);
// called by Knowledge ingestion only when a document lacks embedded text. Stateless, no DB. The
// real engine is a deploy-time native binary; the in-code default reports unavailable (503).

var builder = WebApplication.CreateBuilder(args);

builder.AddKorkutObservability("Korkut.Ocr", tracing => tracing.AddAspNetCoreInstrumentation());

// Web foundation shared with the other hosts: JSON contract, error mapping, CORS, rate limiting.
builder.Services.AddKorkutWeb(builder.Configuration);

// The OCR engine. The in-code scaffold reports unavailable; a real Tesseract adapter replaces
// this (TryAdd) at deploy without touching the endpoint. Tests swap in a fake engine.
builder.Services.TryAddSingleton<IOcrEngine, DisabledOcrEngine>();

// Access-token auth + deny-by-default permission policies (matters.read guards /extract).
builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

// Scanned documents can be large; allow a generous request body for /extract.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 30 * 1024 * 1024);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Ocr", status = "ok" }));
app.MapOcrEndpoints();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed so a WebApplicationFactory<Program> can boot the OCR host in integration tests.
public partial class Program;
