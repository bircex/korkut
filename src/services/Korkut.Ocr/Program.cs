using Korkut.Contract.Documents;
using Korkut.Contract.Documents.OCR;
using Korkut.Web;
using Korkut.Web.Controllers;
using Korkut.Web.Auth;
using Korkut.Documents.Services;
using Korkut.Infrastructure.Observability;
using Korkut.Security;
using Korkut.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// A local install needs no configuration: secrets missing from the environment are generated
// once into the shared secrets volume and reused (see LocalSecrets).
builder.Configuration.AddKorkutLocalSecrets();

builder.AddKorkutObservability("Korkut.Ocr", tracing => tracing.AddAspNetCoreInstrumentation());

builder.Services.AddKorkutWeb(builder.Configuration);
builder.Services.AddKorkutControllers();

// Wire the real Tesseract engine when KORKUT_OCR_ENGINE=tesseract (the image must ship the native
// binaries — see docker/Dockerfile.ocr). Registered BEFORE the scaffold so TryAdd keeps it. Any other
// value (or unset) leaves the DisabledOcrService scaffold (→ 503), so the host still boots anywhere.
if (string.Equals(builder.Configuration["KORKUT_OCR_ENGINE"], "tesseract", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IOcrService>(_ => new TesseractOcrService(builder.Configuration["KORKUT_OCR_LANGS"]));
}

builder.Services.TryAddSingleton<IOcrService, DisabledOcrService>();

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 30 * 1024 * 1024);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Ocr", status = "ok" }));
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
