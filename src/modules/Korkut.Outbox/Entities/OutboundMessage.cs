using Korkut.Core;

namespace Korkut.Outbox.Entities;

/// <summary>
/// A message the assistant drafted for a human to review before it leaves the office.
/// The lifecycle is the point of the aggregate: only a draft may be edited or confirmed,
/// and only a message that has not been sent may be cancelled.
/// </summary>
public sealed class OutboundMessage : Entity, ITenantOwned, IAggregateRoot
{
    public const string Draft = "draft";
    public const string Queued = "queued";
    public const string Cancelled = "cancelled";
    public const string Sent = "sent";

    public OutboundMessage(
        Guid officeId, string kind, string toAddr, string subject, string body,
        DateTimeOffset? sendAt, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Kind = kind;
        ToAddr = toAddr;
        Subject = subject;
        Body = body;
        SendAt = sendAt;
        Status = Draft;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private OutboundMessage()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>"email" | "telegram" | "whatsapp".</summary>
    public string Kind { get; private set; } = "email";

    public string ToAddr { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>When set, the message is held until this instant.</summary>
    public DateTimeOffset? SendAt { get; private set; }

    /// <summary>"draft" | "queued" | "cancelled" | "sent".</summary>
    public string Status { get; private set; } = Draft;

    public string? Error { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public bool IsDraft => Status == Draft;

    public bool IsSent => Status == Sent;

    /// <summary>Edits the message; only a draft may be edited.</summary>
    public bool EditDraft(string toAddr, string subject, string body, DateTimeOffset? sendAt)
    {
        if (!IsDraft)
        {
            return false;
        }

        ToAddr = toAddr;
        Subject = subject;
        Body = body;
        SendAt = sendAt;
        return true;
    }

    /// <summary>Releases the draft for sending; only a draft may be confirmed.</summary>
    public bool Confirm()
    {
        if (!IsDraft)
        {
            return false;
        }

        Status = Queued;
        return true;
    }

    /// <summary>Cancels the message; anything already sent cannot be recalled.</summary>
    public bool Cancel()
    {
        if (IsSent)
        {
            return false;
        }

        Status = Cancelled;
        return true;
    }
}
