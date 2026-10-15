using System.Collections.Concurrent;
using Korkut.Core;
using Korkut.Organization.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Korkut.Organization.Services;

/// <summary>
/// <see cref="IRolePermissions"/> over the office's roles, cached for a minute per office and
/// dropped on any change. Runs in its own scope, because the caller (the sign-in handler) has none
/// yet. The owner's role always grants everything, whatever its row says. If the roles cannot be
/// read, a role grants nothing for that request: never a default the office may have narrowed.
/// </summary>
internal sealed partial class RolePermissions(IServiceScopeFactory scopes, IClock clock, ILogger<RolePermissions> logger) : IRolePermissions
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset Expires, IReadOnlyDictionary<string, IReadOnlySet<string>> Roles)> _offices = new();

    public async Task<IReadOnlySet<string>> ForRoleAsync(Guid officeId, string roleKey, CancellationToken ct = default)
    {
        if (roleKey == Roles.OfficeOwner)
        {
            return Permissions.All;
        }

        IReadOnlyDictionary<string, IReadOnlySet<string>> roles;
        try
        {
            roles = await RolesAsync(officeId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Unreadable(logger, ex);
            return new HashSet<string>();
        }

        if (roles.TryGetValue(roleKey, out var granted))
        {
            return granted;
        }

        return Roles.BuiltIn.TryGetValue(roleKey, out var builtIn) ? builtIn : new HashSet<string>();
    }

    public void Invalidate(Guid officeId) => _offices.TryRemove(officeId, out _);

    private async Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> RolesAsync(Guid officeId, CancellationToken ct)
    {
        if (_offices.TryGetValue(officeId, out var cached) && cached.Expires > clock.UtcNow)
        {
            return cached.Roles;
        }

        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var rows = await services.GetRequiredService<IUnitOfWork>().InTenantScopeAsync(
            officeId, () => services.GetRequiredService<IOfficeRoleRepository>().ListAsync(officeId, ct), ct);
        var roles = rows.ToDictionary(r => r.Key, r => (IReadOnlySet<string>)r.Permissions.Where(Permissions.All.Contains).ToHashSet());
        _offices[officeId] = (clock.UtcNow + Lifetime, roles);
        return roles;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The office's roles could not be read; the request gets no permissions.")]
    private static partial void Unreadable(ILogger logger, Exception ex);
}
