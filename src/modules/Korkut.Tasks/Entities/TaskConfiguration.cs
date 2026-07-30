using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Tasks.Entities;

/// <summary>EF mapping for office agenda records (görev / hatırlatma / randevu).</summary>
public sealed class TaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Category).HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Type).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Location).HasMaxLength(400);
        builder.Property(x => x.Rrule).HasMaxLength(400);
        builder.Property(x => x.ReminderStatus).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_tasks_office_status");
        builder.HasIndex(x => new { x.OfficeId, x.DueAt }).HasDatabaseName("ix_tasks_office_due");
    }
}
