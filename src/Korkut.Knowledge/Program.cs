using Korkut.Knowledge.Endpoints;
using Korkut.Knowledge.Setup;
using Korkut.Web;
using Korkut.Web.Auth;
using Korkut.Infrastructure.Ai;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using OpenTelemetry.Trace;

// Korkut.Knowledge — the stateless RAG host. Owns the vector store (pgvector, schema
// korkut_knowledge) and evolves independently of the Agent: it ingests document text
// (chunk → embed → store, stamped with an embedding version) and answers /search with the
// grounded chunks the Agent retrieves at chat time. Internal-only (the gateway keeps it
// in-cluster). Shares the Application/Infrastructure/Web libraries; never migrates on start.

var builder = WebApplication.CreateBuilder(args);

// Observability: OTel traces/metrics + redacted logs, plus ASP.NET Core request instrumentation.
builder.AddKorkutObservability("Korkut.Knowledge", tracing => tracing.AddAspNetCoreInstrumentation());

// Web foundation shared with the other hosts: JSON contract, versioning, error mapping, CORS.
builder.Services.AddKorkutWeb(builder.Configuration);

// The Knowledge DB (chunks + embeddings), schema korkut_knowledge, own RLS + migration chain +
// pgvector, over the least-privileged app-role connection. Applied only by Korkut.Migrator.
var knowledgeDbUrl = builder.Configuration["KORKUT_KNOWLEDGE_DB_URL"]
    ?? throw new InvalidOperationException("KORKUT_KNOWLEDGE_DB_URL is required for the Knowledge host (the Knowledge DB app-role connection).");
builder.Services.AddKorkutKnowledgePersistence(knowledgeDbUrl);

// The embedder (real OpenAI-compatible adapter when an endpoint is configured, else disabled).
builder.Services.AddKorkutAi();

// Chunker + vector store + ingestion/retrieval use-case services.
builder.Services.AddKorkutKnowledgeServices();

// Clock (row timestamps) + access-token auth + deny-by-default permission policies (same scheme
// as the other hosts, so a token minted by the API/Agent authenticates here too).
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
app.MapKnowledgeEndpoints();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed so a WebApplicationFactory<Program> can boot the knowledge host in integration tests.
public partial class Program;
