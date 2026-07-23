using Korkut.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Courts;

/// <summary>EF mapping for courts (table/columns unchanged).</summary>
public sealed class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.ToTable("courts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(400);
        builder.HasIndex(x => x.OfficeId).HasDatabaseName("ix_courts_office");
    }
}
