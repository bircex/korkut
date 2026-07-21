using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for the correspondence register — split out of OnModelCreating (Clean Architecture layout).</summary>
public sealed class CorrespondenceConfiguration : IEntityTypeConfiguration<CorrespondenceEntity>
{
    public void Configure(EntityTypeBuilder<CorrespondenceEntity> builder)
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
