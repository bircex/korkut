using Korkut.Core;

namespace Korkut.Knowledge.Host.Setup;

/// <summary>
/// The Knowledge host's own plumbing on top of the portable Knowledge module (which registers
/// the pgvector store + ingestion/retrieval services + its unit of work via
/// <c>AddKorkutKnowledge</c>). Here we only bind the caller identity from the HTTP request; the
/// office comes from the token and the tenant scope is opened over the Knowledge DB.
/// </summary>
public static class KnowledgeHostSetup
{
    public static IServiceCollection AddKnowledgeHost(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, Korkut.Web.Identity.HttpCurrentUser>();
        return services;
    }
}
