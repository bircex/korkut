using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Agent.Drafting.Entities;

/// <summary>EF mapping for AI-drafted documents in the Agent DB (table/columns/index unchanged).</summary>
public sealed class GeneratedDocumentConfiguration : IEntityTypeConfiguration<GeneratedDocument>
{
    public void Configure(EntityTypeBuilder<GeneratedDocument> builder)
    {
        builder.ToTable("generated_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Ignore(x => x.HasPending);
        builder.HasIndex(x => new { x.OfficeId, x.ConversationId }).IsUnique().HasDatabaseName("ux_generated_documents_conversation");
    }
}
