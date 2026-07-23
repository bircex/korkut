using Korkut.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Tasks;

/// <summary>EF mapping for office tasks (table/columns unchanged).</summary>
public sealed class TaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_tasks_office_status");
    }
}
