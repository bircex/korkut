using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Korkut.Core;
using Korkut.Pii;

namespace Korkut.Audit;

/// <summary>
/// Writes audit entries to the append-only <c>audit_events</c> table. Before/after
/// snapshots are serialized then run through <see cref="SensitiveDataRedactor"/>, so
/// secrets/tokens/PII never land in the audit trail. The record is INSERT-only — the
/// app role has no UPDATE/DELETE and a trigger blocks mutation.
/// </summary>
public sealed class AuditLog(DbContext db, IClock clock) : IAuditLog
{
    private static readonly JsonSerializerOptions Json = new();

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        db.Set<AuditEvent>().Add(new AuditEvent(
            entry.OfficeId, entry.ActorId, entry.SessionId, entry.CorrelationId,
            entry.Action, entry.EntityType, entry.EntityId,
            Redact(entry.Before), Redact(entry.After), clock.UtcNow));
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
