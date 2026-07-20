using System.Text.Json;
using Korkut.Application.Auditing;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Redaction;
using Korkut.SharedKernel.Time;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Writes audit entries to the append-only <c>audit_events</c> table. Before/after
/// snapshots are serialized then run through <see cref="SensitiveDataRedactor"/>, so
/// secrets/tokens/PII never land in the audit trail. The record is INSERT-only — the
/// app role has no UPDATE/DELETE and a trigger blocks mutation.
/// </summary>
public sealed class EfAuditLog(KorkutDbContext db, IClock clock) : IAuditLog
{
    private static readonly JsonSerializerOptions Json = new();

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        db.AuditEvents.Add(new AuditEventEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = entry.OfficeId,
            ActorId = entry.ActorId,
            SessionId = entry.SessionId,
            CorrelationId = entry.CorrelationId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            BeforeJson = Redact(entry.Before),
            AfterJson = Redact(entry.After),
            At = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Redact(object? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(snapshot, Json);
        return SensitiveDataRedactor.Redact(json);
    }
}
