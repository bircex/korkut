using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Reminders.Entities;

/// <summary>EF mapping for sent reminders (one row per item and due moment).</summary>
public sealed class SentReminderConfiguration : IEntityTypeConfiguration<SentReminder>
{
    public void Configure(EntityTypeBuilder<SentReminder> builder)
    {
        builder.ToTable("sent_reminders");
        builder.HasKey(x => new { x.OfficeId, x.Key });
        builder.Property(x => x.Key).HasMaxLength(120);
        builder.Property(x => x.Kind).HasMaxLength(20);
    }
}
