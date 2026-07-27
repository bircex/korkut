using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Calendar.Entities;

/// <summary>EF mapping for editable calendar events (table/columns unchanged).</summary>
public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Category).HasMaxLength(20);
        builder.Property(x => x.Type).HasMaxLength(100);
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.Location).HasMaxLength(400);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Result).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Rrule).HasMaxLength(400);
        builder.Property(x => x.ReminderStatus).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.StartAt }).HasDatabaseName("ix_calendar_events_office_start");
    }
}
