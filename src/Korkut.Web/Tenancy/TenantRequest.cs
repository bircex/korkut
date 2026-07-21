using System.Security.Claims;
using Korkut.Web.Auth;
using Korkut.Web.Errors;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Web.Tenancy;

/// <summary>
/// Bridges an authenticated request to a tenant-pinned database scope. The office is
/// read from the TRUSTED access-token claim (never a DTO or query param), then work
/// runs inside <see cref="Tenancy.BeginTenantScopeAsync"/> so Postgres RLS applies
/// for the whole unit of work. This is the single place endpoints acquire tenant DB
/// access, so no endpoint can accidentally skip the tenant guard.
/// </summary>
public static class TenantRequest
{
    /// <summary>The signed-in office (tenant) from the access token.</summary>
    public static Guid OfficeId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AccessTokenAuthHandler.OfficeClaim);
        return Guid.TryParse(raw, out var id)
            ? id
            : throw ApiException.Unauthorized("Oturum bulunamadı.");
    }

    /// <summary>The signed-in user id from the access token.</summary>
    public static Guid UserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id)
            ? id
            : throw ApiException.Unauthorized("Oturum bulunamadı.");
    }

    /// <summary>
    /// True if the token carries the given permission. Use only for shaping a response
    /// (e.g. a <c>can_edit</c> hint) — actual access is enforced by
    /// <c>RequireAuthorization</c>, never by an inline check.
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
        => user.HasClaim(AccessTokenAuthHandler.PermissionClaim, permission);

    /// <summary>
    /// The caller's full effective permission set from the access token. Passed to the agent tool
    /// loop so tool RBAC matches the HTTP permission model (a tool the caller can't use is hidden and
    /// refused). Enforcement of endpoint access still lives in <c>RequireAuthorization</c>.
    /// </summary>
    public static IReadOnlySet<string> Permissions(this ClaimsPrincipal user)
        => user.FindAll(AccessTokenAuthHandler.PermissionClaim).Select(c => c.Value).ToHashSet();

    /// <summary>
    /// Run <paramref name="work"/> inside a tenant-pinned transaction and commit it.
    /// The office comes from the caller's token, so RLS scopes every query/command.
    /// </summary>
    public static async Task<T> InTenantScopeAsync<TDb, T>(
        this ClaimsPrincipal user, TDb db, Func<Guid, Task<T>> work, CancellationToken ct = default)
        where TDb : DbContext
    {
        var office = user.OfficeId();
        await using var scope = await db.BeginTenantScopeAsync(office, ct);
        var result = await work(office);
        await scope.CommitAsync(ct);
        return result;
    }
}
