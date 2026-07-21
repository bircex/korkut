using Korkut.Infrastructure.Persistence.Entities;
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

        modelBuilder.Entity<KnowledgeChunkEntity>(b =>
        {
            b.ToTable("chunks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Text).HasColumnType("text");
            b.Property(x => x.EmbeddingVersion).HasMaxLength(100);
            // Dimension is left unspecified so any configured embedder fits; exact cosine search
            // needs no fixed dimension. A fixed-dimension hnsw/ivfflat index is a later optimization.
            b.Property(x => x.Embedding).HasColumnType("vector");
            b.HasIndex(x => new { x.OfficeId, x.DocumentId, x.Ordinal }).HasDatabaseName("ix_chunks_document");
        });

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
