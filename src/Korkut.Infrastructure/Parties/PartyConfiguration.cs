using Korkut.Domain.Matters;
using Korkut.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Parties;

/// <summary>EF mapping for matter parties (tenant-aware composite FK to the matter).</summary>
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
