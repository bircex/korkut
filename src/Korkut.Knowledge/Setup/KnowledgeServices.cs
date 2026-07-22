using Korkut.Application.Features.Knowledge;
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
        return services;
    }
}
