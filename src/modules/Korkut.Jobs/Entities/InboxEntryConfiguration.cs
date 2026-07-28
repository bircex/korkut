
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Jobs.Entities;

/// <summary>EF mapping for the idempotency ledger (table/columns unchanged).</summary>
public sealed class InboxEntryConfiguration : IEntityTypeConfiguration<InboxEntry>
{
    public void Configure(EntityTypeBuilder<InboxEntry> builder)
    {
        builder.ToTable("inbox_entries");
        // Composite PK enforces at-most-once per (office, key) — the idempotency guarantee.
        builder.HasKey(x => new { x.OfficeId, x.IdempotencyKey });
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
    }
}
