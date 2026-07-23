using Korkut.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Settings;

/// <summary>EF mapping for office memory items (table/columns/index unchanged).</summary>
public sealed class MemoryItemConfiguration : IEntityTypeConfiguration<MemoryItem>
{
    public void Configure(EntityTypeBuilder<MemoryItem> builder)
    {
        builder.ToTable("memory_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(50);
        builder.Property(x => x.Text).HasMaxLength(4000);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_memory_items_office_status");
    }
}
