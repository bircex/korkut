using Korkut.Application.Documents;
using Korkut.Application.Documents.OCR;
using Korkut.Web;
using Korkut.Web.Controllers;
using Korkut.Web.Auth;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Security;
using Korkut.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddKorkutObservability("Korkut.Ocr", tracing => tracing.AddAspNetCoreInstrumentation());

builder.Services.AddKorkutWeb(builder.Configuration);
builder.Services.AddKorkutControllers();

builder.Services.TryAddSingleton<IOcrEngine, DisabledOcrEngine>();

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
