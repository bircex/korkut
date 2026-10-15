using System.Security.Cryptography;
using Korkut.Contract;
using Korkut.Contract.Audit;
using Korkut.Contract.Organization;
using Korkut.Core;
using Korkut.Organization.Entities;
using Korkut.Organization.Repositories;

namespace Korkut.Organization.Services;

/// <summary>
/// The office's roles. System roles are created for every office and can be renamed and given
/// other permissions (the owner's grants stay complete); roles the office adds can also be removed
/// once nobody holds them. Every change drops the cached permissions, so it applies at once.
/// </summary>
internal sealed class RoleService(
    IOfficeRoleRepository roles, IUserRepository users, IRolePermissions rolePermissions, IAuditLog audit,
    IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IRoleService
{
    public Task<Result<RolesResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await EnsureAsync(office, ct);
            var rows = await roles.ListAsync(office, ct);
            var counts = await users.CountByRoleAsync(office, ct);
            var ordered = rows
                .OrderBy(r => r.IsSystem ? SystemOrder(r.Key) : int.MaxValue)
                .ThenBy(r => r.CreatedAt)
                .Select(r => ToResponse(r, counts.GetValueOrDefault(r.Key)))
                .ToList();
            return Result<RolesResponse>.Ok(new RolesResponse(PermissionCatalog.Groups, ordered));
        }, ct);

    public Task<Result<RoleResponse>> CreateAsync(SaveRoleRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (Unknown(request.Permissions!) is { Count: > 0 } unknown)
            {
                return Result<RoleResponse>.Unprocessable($"Tanınmayan izin: {string.Join(", ", unknown)}");
            }

            var role = new OfficeRole(
                office, $"custom-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}", request.Name!.Trim(),
                Blank(request.Description), request.Permissions!, isSystem: false, clock.UtcNow);
            roles.Add(role);
            await roles.SaveChangesAsync(ct);
            await RecordAsync(office, "role.created", role, ct);
            rolePermissions.Invalidate(office);
            return Result<RoleResponse>.Ok(ToResponse(role, 0));
        }, ct);

    public Task<Result<RoleResponse>> UpdateAsync(string key, SaveRoleRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (Unknown(request.Permissions!) is { Count: > 0 } unknown)
            {
                return Result<RoleResponse>.Unprocessable($"Tanınmayan izin: {string.Join(", ", unknown)}");
            }

            await EnsureAsync(office, ct);
            var role = await roles.FindForUpdateAsync(office, key, ct);
            if (role is null)
            {
                return Result<RoleResponse>.NotFound("Rol bulunamadı.");
            }

            role.Update(request.Name!.Trim(), Blank(request.Description), request.Permissions!, clock.UtcNow);
            await roles.SaveChangesAsync(ct);
            await RecordAsync(office, "role.changed", role, ct);
            rolePermissions.Invalidate(office);
            var counts = await users.CountByRoleAsync(office, ct);
            return Result<RoleResponse>.Ok(ToResponse(role, counts.GetValueOrDefault(role.Key)));
        }, ct);

    public Task<Result> RemoveAsync(string key, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            await EnsureAsync(office, ct);
            var role = await roles.FindForUpdateAsync(office, key, ct);
            if (role is null)
            {
                return Result.NotFound("Rol bulunamadı.");
            }

            if (role.IsSystem)
            {
                return Result.Conflict("Hazır roller silinemez; izinlerini değiştirebilirsiniz.");
            }

            if ((await users.CountByRoleAsync(office, ct)).GetValueOrDefault(key) > 0)
            {
                return Result.Conflict("Bu rolde üye var. Önce üyelere başka bir rol verin.");
            }

            roles.Remove(role);
            await roles.SaveChangesAsync(ct);
            await RecordAsync(office, "role.removed", role, ct);
            rolePermissions.Invalidate(office);
            return Result.Ok();
        }, ct);

    public async Task<bool> ExistsAsync(Guid officeId, string key, CancellationToken ct = default)
        => Roles.BuiltIn.ContainsKey(key) || await roles.FindAsync(officeId, key, ct) is not null;

    public Task EnsureSystemRolesAsync(Guid officeId, CancellationToken ct = default)
        => unitOfWork.InTenantScopeAsync(officeId, async () =>
        {
            await EnsureAsync(officeId, ct);
            return true;
        }, ct);

    /// <summary>Adds the system roles the office is missing (an install from before roles were stored).</summary>
    private async Task EnsureAsync(Guid office, CancellationToken ct)
    {
        var existing = (await roles.ListAsync(office, ct)).Select(r => r.Key).ToHashSet();
        var missing = Roles.BuiltIn.Where(r => !existing.Contains(r.Key)).ToList();
        foreach (var (key, permissions) in missing)
        {
            roles.Add(new OfficeRole(office, key, Roles.Names[key], null, permissions, isSystem: true, clock.UtcNow));
        }

        if (missing.Count > 0)
        {
            await roles.SaveChangesAsync(ct);
            rolePermissions.Invalidate(office);
        }
    }

    private Task RecordAsync(Guid office, string action, OfficeRole role, CancellationToken ct)
        => audit.WriteAsync(new AuditEntry(
            office, currentUser.UserId, null, null, action, "role", role.Id, null,
            new { role.Key, role.Name, role.Permissions }), ct);

    private static List<string> Unknown(IEnumerable<string> permissions) => [.. permissions.Where(p => !Permissions.All.Contains(p))];

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int SystemOrder(string key)
        => key switch
        {
            Roles.OfficeOwner => 0,
            Roles.Lawyer => 1,
            Roles.Intern => 2,
            Roles.Secretary => 3,
            Roles.Accounting => 4,
            _ => 5,
        };

    private static RoleResponse ToResponse(OfficeRole role, int members)
        => new(role.Key, role.Name, role.Description, role.Key == Roles.OfficeOwner ? [.. Permissions.All.Order()] : role.Permissions,
            role.IsSystem, Editable: role.Key != Roles.OfficeOwner, members);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }
}
