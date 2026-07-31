using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Chat.Entities;

/// <summary>EF mapping for chat messages in the Agent DB (table/columns/index unchanged).</summary>
public sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Role).HasMaxLength(20);
        builder.Property(x => x.SourcesJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.OfficeId, x.ConversationId, x.CreatedAt }).HasDatabaseName("ix_chat_messages_conversation");
    }
}
