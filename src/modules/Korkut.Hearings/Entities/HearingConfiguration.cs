using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Hearings.Entities;

/// <summary>
/// EF mapping for hearings (table unchanged). The tenant-aware composite FK to the matter is a
/// cross-module relationship configured in Infrastructure (a module never references another
/// module's entity type).
/// </summary>
public sealed class HearingConfiguration : IEntityTypeConfiguration<Hearing>
{
    public void Configure(EntityTypeBuilder<Hearing> builder)
    {
        builder.ToTable("matter_hearings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasMaxLength(100);
        builder.Property(x => x.Location).HasMaxLength(400);
        builder.Property(x => x.Result).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.ReminderStatus).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_matter_hearings_office_matter");
    }
}
