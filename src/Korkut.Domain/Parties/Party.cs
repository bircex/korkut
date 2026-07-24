using Korkut.Core;

namespace Korkut.Domain.Parties;

/// <summary>A party/counsel attached to a matter (tenant-owned; composite FK to the matter).</summary>
public sealed class Party : Entity, ITenantOwned, IAggregateRoot
{
    public Party(Guid officeId, Guid matterId, string role, string name, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        MatterId = matterId;
        Role = role;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Party()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid MatterId { get; private set; }

    /// <summary>"plaintiff" | "defendant" | "counsel" | ...</summary>
    public string Role { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
