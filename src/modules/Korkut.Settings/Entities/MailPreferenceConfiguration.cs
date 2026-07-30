using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Settings.Entities;

/// <summary>EF mapping for the office mail preference (keyed by office, one row per office).</summary>
public sealed class MailPreferenceConfiguration : IEntityTypeConfiguration<MailPreference>
{
    public void Configure(EntityTypeBuilder<MailPreference> builder)
    {
        builder.ToTable("mail_preferences");
        builder.HasKey(x => x.OfficeId); // exactly one mail-preference row per office
        builder.Property(x => x.OfficeId).ValueGeneratedNever();
        builder.Property(x => x.NotificationsEnabled);
        builder.Property(x => x.NotifyEmail).HasMaxLength(320);
    }
}
