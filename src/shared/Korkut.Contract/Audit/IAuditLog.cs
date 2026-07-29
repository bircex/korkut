namespace Korkut.Contract.Audit;

/// <summary>
/// One audit entry. Actor/session/correlation/office come from the trusted server
/// context — callers must not populate them from client input. <c>Before</c>/
/// <c>After</c> are arbitrary snapshots that the sink serializes and <b>redacts</b>;
/// never pass secrets, tokens, or raw document bodies.
/// </summary>
public sealed record AuditEntry(
    Guid OfficeId,
    Guid? ActorId,
    string? SessionId,
    string? CorrelationId,
    string Action,
    string EntityType,
    Guid? EntityId,
    object? Before,
    object? After);

/// <summary>Append-only audit sink for sensitive reads/writes/downloads/admin/recovery.</summary>
public interface IAuditLog
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
