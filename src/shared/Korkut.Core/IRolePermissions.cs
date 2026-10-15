namespace Korkut.Core;

/// <summary>
/// What a role grants in an office, read from the office's roles (a role without a row grants its
/// built-in default; an unknown role grants nothing). Cached briefly, and dropped as soon as a role
/// changes, so an edited role takes effect on the next request.
/// </summary>
public interface IRolePermissions
{
    Task<IReadOnlySet<string>> ForRoleAsync(Guid officeId, string roleKey, CancellationToken ct = default);

    /// <summary>Forget the office's cached roles (after one was created, changed or removed).</summary>
    void Invalidate(Guid officeId);
}
