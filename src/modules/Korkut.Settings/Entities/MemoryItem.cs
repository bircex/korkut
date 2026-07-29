using Korkut.Core;

namespace Korkut.Settings.Entities;

/// <summary>
/// A remembered fact or instruction for the office assistant. Who added it decides where it
/// starts: an owner's item is active at once, a member's waits for approval.
/// </summary>
public sealed class MemoryItem : Entity, ITenantOwned, IAggregateRoot
{
    public const string Active = "active";
    
    public const string Pending = "pending";

    public MemoryItem(Guid officeId, string text, string kind, Guid? authorId, bool active, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Text = text;
        Kind = kind;
        AuthorId = authorId;
        Status = active ? Active : Pending;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private MemoryItem()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>Free-form category, e.g. "client" | "court" | "preference".</summary>
    public string Kind { get; private set; } = "note";

    public string Text { get; private set; } = string.Empty;

    /// <summary>"active" | "pending".</summary>
    public string Status { get; private set; } = Pending;

    /// <summary>The user who added it (for attribution).</summary>
    public Guid? AuthorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Promotes a pending item to active; approving an active item changes nothing.</summary>
    public void Approve() => Status = Active;
}
