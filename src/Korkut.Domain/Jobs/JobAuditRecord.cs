using Korkut.Domain.Common;

namespace Korkut.Domain.Jobs;

/// <summary>
/// One line in the job trail: what happened to a message and when. Append-only — created
/// complete, with no way to change it afterwards.
/// </summary>
public sealed class JobAuditRecord : Entity, ITenantOwned, IAggregateRoot
{
    public JobAuditRecord(
        Guid messageId, Guid officeId, string type, JobAuditKind kind, string? detail, DateTimeOffset at)
        : base(NewId())
    {
        MessageId = messageId;
        OfficeId = officeId;
        Type = type;
        Kind = kind;
        Detail = detail;
        At = at;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private JobAuditRecord()
    {
    }

    public Guid MessageId { get; private set; }

    public Guid OfficeId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public JobAuditKind Kind { get; private set; }

    public string? Detail { get; private set; }

    public DateTimeOffset At { get; private set; }
}
