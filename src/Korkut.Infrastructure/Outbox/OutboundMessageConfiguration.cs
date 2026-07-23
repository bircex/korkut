using Korkut.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Outbox;

/// <summary>EF mapping for outbound messages (table/columns unchanged).</summary>
public sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> builder)
    {
        builder.ToTable("outbound_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.ToAddr).HasMaxLength(400);
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Ignore(x => x.IsDraft);
        builder.Ignore(x => x.IsSent);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_outbound_messages_office_status");
    }
}
