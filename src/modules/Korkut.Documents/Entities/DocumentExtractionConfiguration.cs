using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Documents.Entities;

/// <summary>EF mapping for extracted text spans (table/columns/index/FK unchanged).</summary>
public sealed class DocumentExtractionConfiguration : IEntityTypeConfiguration<DocumentExtraction>
{
    public void Configure(EntityTypeBuilder<DocumentExtraction> builder)
    {
        builder.ToTable("document_extractions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ParserVersion).HasMaxLength(50);
        builder.Property(x => x.SourceChecksumSha256).HasMaxLength(64);
        builder.Property(x => x.SourceObjectKey).HasMaxLength(100);
        // Tenant-aware composite FK to the source document.
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.DocumentId })
            .HasPrincipalKey(d => new { d.OfficeId, d.Id })
            .OnDelete(DeleteBehavior.Cascade);
        // Retry idempotency + immutable-per-parser: one row per source span + parser.
        builder.HasIndex(x => new { x.OfficeId, x.DocumentId, x.DocumentVersion, x.ParserVersion, x.Page, x.SpanIndex })
            .IsUnique()
            .HasDatabaseName("ux_doc_extractions_provenance");
    }
}
