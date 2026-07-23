using Korkut.Domain.Common;
namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only who-did-what-when record for legal/administrative data. Immutable at
/// the database (the app role has INSERT/SELECT only, and a trigger blocks
/// UPDATE/DELETE). Actor/session/correlation come from the trusted server context,
/// never from client input. before/after are redacted — no secrets, tokens or raw
/// document bodies.
/// </summary>
public sealed class AuditEventEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>The authenticated actor (user) — set by the server, not the client.</summary>
    public Guid? ActorId { get; set; }

    public string? SessionId { get; set; }

    public string? CorrelationId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>Redacted JSON snapshot before the change (null for creates).</summary>
    public string? BeforeJson { get; set; }

    /// <summary>Redacted JSON snapshot after the change (null for deletes).</summary>
    public string? AfterJson { get; set; }

    public DateTimeOffset At { get; set; }
}
