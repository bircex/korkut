using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Settings;

/// <summary>EF mapping for drafting-style profiles (table/columns/index unchanged).</summary>
public sealed class StyleProfileConfiguration : IEntityTypeConfiguration<StyleProfile>
{
    public void Configure(EntityTypeBuilder<StyleProfile> builder)
    {
        builder.ToTable("style_profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Scope).HasMaxLength(10);
        builder.Property(x => x.Letterhead).HasMaxLength(2000);
        builder.Property(x => x.Signature).HasMaxLength(2000);
        builder.Property(x => x.Tone).HasMaxLength(2000);
        builder.Property(x => x.Boilerplate).HasMaxLength(4000);
        builder.Ignore(x => x.Fields);
        // One office profile, and at most one per user, per office.
        builder.HasIndex(x => new { x.OfficeId, x.Scope, x.UserId })
            .IsUnique().HasDatabaseName("ux_style_profiles_office_scope_user");
    }
}
