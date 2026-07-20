namespace Korkut.Application.Security;

/// <summary>
/// Resolves the effective permission set for a principal and answers permission
/// checks — deny-by-default. A principal's permissions are the union of its role's
/// built-in set and any explicit custom-role permissions. Unknown roles grant
/// nothing.
/// </summary>
public sealed class AuthorizationService
{
    // Injected as a service (composition/testability), so keep instance methods.
#pragma warning disable CA1822 // Mark members as static
    public IReadOnlySet<string> ResolvePermissions(string role, IEnumerable<string>? customPermissions = null)
    {
        var effective = new HashSet<string>();
        if (Roles.BuiltIn.TryGetValue(role, out var builtin))
        {
            effective.UnionWith(builtin);
        }

        if (customPermissions is not null)
        {
            // Only recognised permissions are honoured — garbage grants nothing.
            effective.UnionWith(customPermissions.Where(Permissions.All.Contains));
        }

        return effective;
    }

    public bool HasPermission(string role, string permission, IEnumerable<string>? customPermissions = null)
        => ResolvePermissions(role, customPermissions).Contains(permission);
#pragma warning restore CA1822
}
