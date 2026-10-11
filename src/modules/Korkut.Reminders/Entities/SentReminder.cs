using Korkut.Core;

namespace Korkut.Reminders.Entities;

/// <summary>
/// A reminder that went out. The key names the item and the moment it was due, so each reminder is
/// sent once, and moving a date sets up a new one.
/// </summary>
public sealed class SentReminder : ITenantOwned, IAggregateRoot
{
    public SentReminder(Guid officeId, string key, string kind, Guid itemId, DateTimeOffset sentAt)
    {
        OfficeId = officeId;
        Key = key;
        Kind = kind;
        ItemId = itemId;
        SentAt = sentAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private SentReminder()
    {
    }

    public Guid OfficeId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Kind { get; private set; } = string.Empty;

    public Guid ItemId { get; private set; }

    public DateTimeOffset SentAt { get; private set; }
}
