using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for tasks — split out of OnModelCreating (Clean Architecture layout).</summary>
public sealed class TaskConfiguration : IEntityTypeConfiguration<TaskEntity>
{
    public void Configure(EntityTypeBuilder<TaskEntity> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_tasks_office_status");
    }
}

/// <summary>EF mapping for deadlines.</summary>
public sealed class DeadlineConfiguration : IEntityTypeConfiguration<DeadlineEntity>
{
    public void Configure(EntityTypeBuilder<DeadlineEntity> builder)
    {
        builder.ToTable("deadlines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.TimeZoneId).HasMaxLength(60);
        builder.HasIndex(x => new { x.OfficeId, x.DueDate }).HasDatabaseName("ix_deadlines_office_due");
    }
}

/// <summary>EF mapping for communication notes.</summary>
public sealed class CommunicationNoteConfiguration : IEntityTypeConfiguration<CommunicationNoteEntity>
{
    public void Configure(EntityTypeBuilder<CommunicationNoteEntity> builder)
    {
        builder.ToTable("communication_notes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Channel).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_comm_notes_office_matter");
    }
}
