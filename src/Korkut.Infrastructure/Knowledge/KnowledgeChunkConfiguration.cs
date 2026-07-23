using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Knowledge;

/// <summary>EF mapping for indexed document chunks (table/columns/index unchanged).</summary>
public sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunkEntity>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunkEntity> builder)
    {
        builder.ToTable("chunks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Text).HasColumnType("text");
        builder.Property(x => x.EmbeddingVersion).HasMaxLength(100);
        // Dimension is left unspecified so any configured embedder fits; exact cosine search needs
        // no fixed dimension. A fixed-dimension hnsw/ivfflat index is a later optimization.
        builder.Property(x => x.Embedding).HasColumnType("vector");
        builder.HasIndex(x => new { x.OfficeId, x.DocumentId, x.Ordinal }).HasDatabaseName("ix_chunks_document");
    }
}
