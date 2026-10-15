using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Organization;

/// <summary>One permission as the roles screen shows it.</summary>
public sealed record PermissionInfo(string Key, string Label, string? Hint = null);

/// <summary>A group of related permissions (a section of the roles matrix).</summary>
public sealed record PermissionGroup(string Name, IReadOnlyList<PermissionInfo> Permissions);

/// <summary>A role in the office, with how many members hold it.</summary>
public sealed record RoleResponse(
    string Key, string Name, string? Description, IReadOnlyList<string> Permissions, bool IsSystem, bool Editable, int MemberCount);

/// <summary>The roles screen: the catalog to lay out the matrix, and the office's roles.</summary>
public sealed record RolesResponse(IReadOnlyList<PermissionGroup> Catalog, IReadOnlyList<RoleResponse> Roles);

/// <summary>Body for creating or changing a role.</summary>
public sealed record SaveRoleRequest
{
    [Required(ErrorMessage = "Rol adı zorunludur.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Rol adı 2 ile 100 karakter arasında olmalıdır.")]
    public string? Name { get; init; }

    [MaxLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "İzinler zorunludur.")]
    public IReadOnlyList<string>? Permissions { get; init; }
}

/// <summary>The office's roles (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IRoleService
{
    Task<Result<RolesResponse>> ListAsync(CancellationToken ct = default);

    Task<Result<RoleResponse>> CreateAsync(SaveRoleRequest request, CancellationToken ct = default);

    Task<Result<RoleResponse>> UpdateAsync(string key, SaveRoleRequest request, CancellationToken ct = default);

    /// <summary>Removes a role the office added. System roles stay, and a role someone holds cannot go.</summary>
    Task<Result> RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Whether the office has this role (a system role always counts).</summary>
    Task<bool> ExistsAsync(Guid officeId, string key, CancellationToken ct = default);

    /// <summary>Creates the system roles the office does not have yet; never changes existing ones.</summary>
    Task EnsureSystemRolesAsync(Guid officeId, CancellationToken ct = default);
}
