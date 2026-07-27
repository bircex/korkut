using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Deadlines;

/// <summary>EF mapping for deadlines (table/columns unchanged).</summary>
public sealed class DeadlineConfiguration : IEntityTypeConfiguration<Deadline>
{
    public void Configure(EntityTypeBuilder<Deadline> builder)
    {
        builder.ToTable("deadlines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.TimeZoneId).HasMaxLength(60);
        builder.HasIndex(x => new { x.OfficeId, x.DueDate }).HasDatabaseName("ix_deadlines_office_due");
    }
}
