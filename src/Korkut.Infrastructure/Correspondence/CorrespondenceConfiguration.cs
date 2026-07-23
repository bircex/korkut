using Korkut.Domain.Correspondence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Correspondence;

/// <summary>EF mapping for the correspondence register (table/columns unchanged).</summary>
public sealed class CorrespondenceConfiguration : IEntityTypeConfiguration<CorrespondenceItem>
{
    public void Configure(EntityTypeBuilder<CorrespondenceItem> builder)
    {
        builder.ToTable("correspondence");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Direction).HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.ReferenceNo).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.OfficeId, x.Direction }).HasDatabaseName("ix_correspondence_office_direction");
    }
}
