using Korkut.Domain.Common;

namespace Korkut.Domain.Courts;

/// <summary>
/// Reference court data with a MUTABLE display title. Matters snapshot the title at link
/// time so a later rename never rewrites historical documents.
/// </summary>
public sealed class Court : Entity, ITenantOwned, IAggregateRoot
{
    public Court(Guid officeId, string name, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Court()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>Mutable display title — may be corrected over time.</summary>
    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public void Rename(string name) => Name = name;
}
