using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Documents.Entities;

/// <summary>EF mapping for document metadata (table/columns/indexes unchanged).</summary>
public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Bucket).HasMaxLength(63);
        builder.Property(x => x.ObjectKey).HasMaxLength(100);
        builder.Property(x => x.OriginalFilename).HasMaxLength(500);
        builder.Property(x => x.ContentType).HasMaxLength(200);
        builder.Property(x => x.ChecksumSha256).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Ignore(x => x.IsTombstoned);
        builder.Ignore(x => x.IsDownloadable);
        builder.HasIndex(x => x.ObjectKey).IsUnique().HasDatabaseName("ux_documents_object_key");
        builder.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_documents_office_matter");
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_documents_office_status");
    }
}
