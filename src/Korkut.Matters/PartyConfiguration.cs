using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Matters;

/// <summary>EF mapping for matter parties (tenant-aware composite FK to the matter, intra-module).</summary>
public sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("parties");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Role).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(400);
        builder.HasOne<Matter>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.MatterId })
            .HasPrincipalKey(m => new { m.OfficeId, m.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_parties_office_matter");
    }
}
