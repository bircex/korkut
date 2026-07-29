using Microsoft.EntityFrameworkCore;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Jobs.Entities;

namespace Korkut.Jobs.Persistence;

/// <summary>PostgreSQL append-only job audit.</summary>
public sealed class JobAudit(DbContext db) : IJobAudit
{
    public async Task RecordAsync(JobAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        db.Set<JobAuditRecord>().Add(new JobAuditRecord(
            auditEvent.MessageId, auditEvent.OfficeId, auditEvent.Type,
            auditEvent.Kind, auditEvent.Detail, auditEvent.At));
        await db.SaveChangesAsync(cancellationToken);
    }
}
