using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// A role in the office: a named permission set that a user's role key points at. The six system
/// roles (<see cref="Roles"/>) are created for every office and can be changed but not removed; the
/// owner's always grants everything. The office can add its own. Only permissions the platform
/// recognises are kept, so an office cannot grant itself something the system does not define.
/// </summary>
public sealed class OfficeRole : Entity, ITenantOwned, IAggregateRoot
{
    public OfficeRole(Guid officeId, string key, string name, string? description, IEnumerable<string> permissions, bool isSystem, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Key = key;
        Name = name;
        Description = description;
        Permissions = Recognised(permissions);
        IsSystem = isSystem;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private OfficeRole()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>Stable key referenced by a user's role (unique per office).</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>The permissions this role grants.</summary>
    public List<string> Permissions { get; private set; } = [];

    /// <summary>One of the roles every office starts with: it can be changed, not removed.</summary>
    public bool IsSystem { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Renames the role and replaces the permissions it grants. The owner's grants stay complete.</summary>
    public void Update(string name, string? description, IEnumerable<string> permissions, DateTimeOffset now)
    {
        Name = name;
        Description = description;
        Permissions = Key == Roles.OfficeOwner ? [.. Korkut.Core.Permissions.All.Order()] : Recognised(permissions);
        UpdatedAt = now;
    }

    private static List<string> Recognised(IEnumerable<string> permissions)
        => [.. permissions.Where(Korkut.Core.Permissions.All.Contains).Distinct().Order()];
}
