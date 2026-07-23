using Korkut.Domain.Common;

namespace Korkut.Domain.OfficeRoles;

/// <summary>
/// A role an office defines for itself: a named permission set that a user's role key points
/// at. Tenant-scoped, so a role and the users it grants can never cross office boundaries. Only
/// permissions the platform recognises are honoured, so an office cannot grant itself something
/// the system does not define.
/// </summary>
public sealed class OfficeRole : Entity, ITenantOwned, IAggregateRoot
{
    public OfficeRole(Guid officeId, string key, string name, IReadOnlyList<string> permissions, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Key = key;
        Name = name;
        Permissions = [.. permissions];
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private OfficeRole()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>Stable key referenced by a user's role (unique per office).</summary>
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>The explicit permission list this role grants.</summary>
    public List<string> Permissions { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Renames the role and replaces the permissions it grants.</summary>
    public void Update(string name, IReadOnlyList<string> permissions)
    {
        Name = name;
        Permissions = [.. permissions];
    }
}
