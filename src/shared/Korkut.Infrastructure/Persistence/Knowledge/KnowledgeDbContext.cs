using Korkut.Persistence;
using Korkut.Infrastructure.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Knowledge;

/// <summary>
/// The Knowledge DB context — the RAG plane's private store (indexed document chunks +
/// embeddings) in its own schema (<c>korkut_knowledge</c>) with its own migration chain and RLS,
/// so it can live in a separate database/instance and evolve off the Agent's release cycle. Owns
/// the <c>vector</c> (pgvector) column type. Same identity + snake_case conventions as the other
/// contexts; tenant context is set per transaction via <see cref="Tenancy.BeginTenantScopeAsync"/>
/// (the shared <c>app.current_office</c> GUC the RLS policy reads).
/// </summary>
public sealed class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : DbContext(options)
{
    public DbSet<KnowledgeChunkEntity> Chunks => Set<KnowledgeChunkEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersistenceSetup.KnowledgeSchema);

        // Emits `CREATE EXTENSION IF NOT EXISTS vector` in the migration (run by the elevated migrator).
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfiguration(new KnowledgeChunkConfiguration());

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
