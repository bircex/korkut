using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Jobs.Entities;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Infrastructure.Jobs;

/// <summary>PostgreSQL append-only job audit.</summary>
public sealed class JobAudit(KorkutDbContext db) : IJobAudit
{
    public async Task RecordAsync(JobAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        db.JobAudit.Add(new JobAuditRecord(
            auditEvent.MessageId, auditEvent.OfficeId, auditEvent.Type,
            auditEvent.Kind, auditEvent.Detail, auditEvent.At));
        await db.SaveChangesAsync(cancellationToken);
    }
}
