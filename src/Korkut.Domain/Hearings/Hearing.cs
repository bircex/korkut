using Korkut.Core;

namespace Korkut.Domain.Hearings;

/// <summary>A hearing / duruşma on a matter's calendar. Reached through its matter, tenant-owned.</summary>
public sealed class Hearing : Entity, ITenantOwned, IAggregateRoot
{
    public Hearing(Guid officeId, Guid matterId, DateTimeOffset scheduledAt, string? type, string? location, string? notes)
        : base(NewId())
    {
        OfficeId = officeId;
        MatterId = matterId;
        ScheduledAt = scheduledAt;
        Type = type;
        Location = location;
        Notes = notes;
        ReminderStatus = "pending";
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Hearing()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid MatterId { get; private set; }

    /// <summary>The hearing instant (date + optional time). Consumed by the calendar/agenda.</summary>
    public DateTimeOffset ScheduledAt { get; private set; }

    /// <summary>Hearing type (e.g. "ön inceleme", "tahkikat").</summary>
    public string? Type { get; private set; }

    public string? Location { get; private set; }

    /// <summary>Outcome recorded after the hearing.</summary>
    public string? Result { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>"pending" | "sent" | "dismissed".</summary>
    public string ReminderStatus { get; private set; } = "pending";
}
