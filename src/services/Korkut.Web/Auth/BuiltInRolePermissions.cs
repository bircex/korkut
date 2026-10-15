using Korkut.Core;

namespace Korkut.Web.Auth;

/// <summary>
/// <see cref="IRolePermissions"/> for a host with no office database (the OCR host): a role grants
/// its built-in default. The app host replaces it with the office's stored roles.
/// </summary>
public sealed class BuiltInRolePermissions : IRolePermissions
{
    public Task<IReadOnlySet<string>> ForRoleAsync(Guid officeId, string roleKey, CancellationToken ct = default)
        => Task.FromResult(Roles.BuiltIn.TryGetValue(roleKey, out var granted) ? granted : (IReadOnlySet<string>)new HashSet<string>());

    public void Invalidate(Guid officeId)
    {
    }
}
