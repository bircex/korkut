using Korkut.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Auditing;

/// <summary>EF mapping for the audit trail (table/columns/indexes unchanged).</summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Action).HasMaxLength(100);
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.SessionId).HasMaxLength(200);
        builder.Property(x => x.CorrelationId).HasMaxLength(200);
        builder.HasIndex(x => new { x.OfficeId, x.At }).HasDatabaseName("ix_audit_events_office_at");
        builder.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("ix_audit_events_entity");
    }
}
