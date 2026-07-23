using Korkut.Domain.Common;

namespace Korkut.Domain.Auditing;

/// <summary>
/// One entry in the office's audit trail. The record is append-only by construction: it is
/// created complete and has no way to change afterwards — the app role holds no UPDATE or
/// DELETE on the table and a trigger blocks mutation, and this type offers no method that
/// would want them. The before/after snapshots arrive already redacted.
/// </summary>
public sealed class AuditEvent : Entity, ITenantOwned, IAggregateRoot
{
    public AuditEvent(
        Guid officeId, Guid? actorId, string? sessionId, string? correlationId,
        string action, string entityType, Guid? entityId,
        string? beforeJson, string? afterJson, DateTimeOffset at)
        : base(NewId())
    {
        OfficeId = officeId;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        At = at;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private AuditEvent()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>The authenticated actor — set by the server, never taken from the client.</summary>
    public Guid? ActorId { get; private set; }

    public string? SessionId { get; private set; }

    public string? CorrelationId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    /// <summary>Redacted JSON snapshot before the change (null for creates).</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>Redacted JSON snapshot after the change (null for deletes).</summary>
    public string? AfterJson { get; private set; }

    public DateTimeOffset At { get; private set; }
}
