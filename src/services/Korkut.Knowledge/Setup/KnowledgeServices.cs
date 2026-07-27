using Korkut.Application.Knowledge;
using Korkut.Infrastructure.Knowledge;

namespace Korkut.Knowledge.Setup;

/// <summary>
/// Registers the Knowledge host's own services: the boundary-aware chunker, the pgvector store,
/// and the ingestion + retrieval use-case services. The embedder itself comes from
/// <c>AddKorkutAi</c> (real OpenAI-compatible adapter when an endpoint is configured, disabled
/// otherwise).
/// </summary>
public static class KnowledgeServices
{
    public static IServiceCollection AddKorkutKnowledgeServices(this IServiceCollection services)
    {
        services.AddSingleton<IChunkerService, ChunkerService>();
        services.AddScoped<IVectorStoreService, PgVectorStoreService>();
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IRetrievalService, RetrievalService>();
        // The office comes from the caller's token, and the tenant scope is opened over the
        // Knowledge DB (its own IUnitOfWork), so the services never take an office parameter.
        services.AddHttpContextAccessor();
        services.AddScoped<Korkut.Core.ICurrentUser, Korkut.Web.Identity.HttpCurrentUser>();
        services.AddScoped<Korkut.Core.IUnitOfWork, Korkut.Infrastructure.Knowledge.KnowledgeUnitOfWork>();
        return services;
    }
}
