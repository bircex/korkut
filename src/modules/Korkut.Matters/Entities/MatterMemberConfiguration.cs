using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Matters.Entities;

/// <summary>EF mapping for matter teams (tenant-aware composite FK to the matter, intra-module).</summary>
public sealed class MatterMemberConfiguration : IEntityTypeConfiguration<MatterMember>
{
    public void Configure(EntityTypeBuilder<MatterMember> builder)
    {
        builder.ToTable("matter_members");
        builder.HasKey(x => new { x.MatterId, x.UserId });
        builder.HasOne<Matter>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.MatterId })
            .HasPrincipalKey(m => new { m.OfficeId, m.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.OfficeId, x.UserId }).HasDatabaseName("ix_matter_members_office_user");
    }
}
