using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Agent.Chat;

/// <summary>EF mapping for assistant conversations in the Agent DB (table/columns/index unchanged).</summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.HasIndex(x => new { x.OfficeId, x.UpdatedAt }).HasDatabaseName("ix_conversations_office_updated");
    }
}
