using Korkut.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Jobs;

/// <summary>EF mapping for outbox messages (table/columns/indexes unchanged).</summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();          // app-assigned UUIDv7
        builder.Property(x => x.Type).HasMaxLength(200);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LeaseOwner).HasMaxLength(200);

        // Query: the dispatcher claims due, unleased messages ordered by AvailableAt.
        builder.HasIndex(x => new { x.Status, x.AvailableAt }).HasDatabaseName("ix_outbox_status_available_at");
        // Query: dedup / idempotency lookups are scoped by office + key.
        builder.HasIndex(x => new { x.OfficeId, x.IdempotencyKey }).HasDatabaseName("ix_outbox_office_key");
    }
}
