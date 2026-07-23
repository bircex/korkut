using Korkut.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Jobs;

/// <summary>EF mapping for the job audit trail (table/columns/indexes unchanged).</summary>
public sealed class JobAuditRecordConfiguration : IEntityTypeConfiguration<JobAuditRecord>
{
    public void Configure(EntityTypeBuilder<JobAuditRecord> builder)
    {
        builder.ToTable("job_audit");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasMaxLength(200);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);

        // Query: operator audit is read per office, newest first.
        builder.HasIndex(x => new { x.OfficeId, x.At }).HasDatabaseName("ix_job_audit_office_at");
        // Query: trace every event for one message.
        builder.HasIndex(x => x.MessageId).HasDatabaseName("ix_job_audit_message");
    }
}
