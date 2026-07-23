using Korkut.Domain.Common;
namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// An office-defined custom role: a named, explicit permission set. Tenant-scoped
/// (RLS), so a role and its assignment can never cross office boundaries.
/// </summary>
public sealed class OfficeCustomRoleEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>Stable key referenced by users.role (unique per office).</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Explicit permission list (only recognised permissions are honoured).</summary>
    public List<string> Permissions { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}
