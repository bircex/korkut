using Korkut.Domain.Common;

namespace Korkut.Domain.Notes;

/// <summary>A record of contact with a client or about a matter (call, e-mail, meeting, note).</summary>
public sealed class CommunicationNote : Entity, ITenantOwned, IAggregateRoot
{
    public CommunicationNote(
        Guid officeId, string body, string channel, Guid? matterId, Guid? clientId,
        DateTimeOffset occurredAt, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Body = body;
        Channel = channel;
        MatterId = matterId;
        ClientId = clientId;
        OccurredAt = occurredAt;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private CommunicationNote()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid? MatterId { get; private set; }

    public Guid? ClientId { get; private set; }

    /// <summary>"note" | "call" | "email" | "meeting".</summary>
    public string Channel { get; private set; } = "note";

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
