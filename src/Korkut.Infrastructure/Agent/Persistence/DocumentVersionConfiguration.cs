using Korkut.Domain.Agent.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Agent.Persistence;

/// <summary>EF mapping for generated-document version snapshots in the Agent DB (table/columns/index unchanged).</summary>
public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Author).HasMaxLength(320);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.OfficeId, x.DocumentId, x.CreatedAt }).HasDatabaseName("ix_document_versions_document");
    }
}
