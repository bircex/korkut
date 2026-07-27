using Korkut.Knowledge;
using Korkut.Knowledge.Host.Setup;
using Korkut.Web;
using Korkut.Web.Controllers;
using Korkut.Web.Auth;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Security;
using Korkut.Core;
using OpenTelemetry.Trace;

// Korkut.Knowledge — the stateless RAG host. Owns the vector store (pgvector, schema
// korkut_knowledge) and evolves independently of the Agent: it ingests document text
// (chunk → embed → store, stamped with an embedding version) and answers /search with the
// grounded chunks the Agent retrieves at chat time. Internal-only (the gateway keeps it
// in-cluster). Shares the Application/Infrastructure/Web libraries; never migrates on start.

var builder = WebApplication.CreateBuilder(args);

builder.AddKorkutObservability("Korkut.Knowledge", tracing => tracing.AddAspNetCoreInstrumentation());

builder.Services.AddKorkutWeb(builder.Configuration);
builder.Services.AddKorkutControllers();

var knowledgeDbUrl = builder.Configuration["KORKUT_KNOWLEDGE_DB_URL"]
    ?? throw new InvalidOperationException("KORKUT_KNOWLEDGE_DB_URL is required for the Knowledge host (the Knowledge DB app-role connection).");
// The portable Knowledge module owns its DbContext + pgvector store + ingestion/retrieval services.
builder.Services.AddKorkutKnowledge(knowledgeDbUrl);

builder.Services.AddKorkutAi();

// Host-only plumbing: the caller identity from the HTTP request.
builder.Services.AddKnowledgeHost();

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton(sp => new AccessTokenService(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddSingleton(sp => new AdminAccessTokens(
    builder.Configuration["KORKUT_SECRET"] ?? string.Empty, sp.GetRequiredService<IClock>()));
builder.Services.AddKorkutAuthorization();

// Ingested document text can be large; allow a generous request body for /ingest.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 20 * 1024 * 1024);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseKorkutWeb();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "Korkut.Knowledge", status = "ok" }));
app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
