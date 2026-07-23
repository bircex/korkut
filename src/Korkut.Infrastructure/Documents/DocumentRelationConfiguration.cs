using Korkut.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Documents;

/// <summary>EF mapping for document relations (table/columns/indexes unchanged).</summary>
public sealed class DocumentRelationConfiguration : IEntityTypeConfiguration<DocumentRelation>
{
    public void Configure(EntityTypeBuilder<DocumentRelation> builder)
    {
        builder.ToTable("document_relations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TargetType).HasMaxLength(20);
        // Tenant-aware composite FK to the source document.
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.DocumentId })
            .HasPrincipalKey(d => new { d.OfficeId, d.Id })
            .OnDelete(DeleteBehavior.Cascade);
        // One link per (document, target) — makes multi-linking idempotent.
        builder.HasIndex(x => new { x.OfficeId, x.DocumentId, x.TargetType, x.TargetId })
            .IsUnique()
            .HasDatabaseName("ux_doc_relations_link");
        builder.HasIndex(x => new { x.OfficeId, x.TargetType, x.TargetId })
            .HasDatabaseName("ix_doc_relations_target");
    }
}
