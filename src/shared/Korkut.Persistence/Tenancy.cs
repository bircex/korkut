using Microsoft.EntityFrameworkCore;

namespace Korkut.Persistence;

/// <summary>
/// Transaction-scoped tenant context. RLS policies read
/// <c>current_setting('app.current_office')</c>; this sets it with
/// <c>set_config(..., is_local := true)</c> so it is bound to the current
/// transaction and can never leak onto the next borrower of a pooled connection.
/// The tenant is established from the trusted session (KDM-018), never from a DTO.
/// </summary>
public static class Tenancy
{
    private const string SettingName = "app.current_office";

    /// <summary>
    /// Begin a transaction and pin the tenant for its duration. Dispose commits;
    /// callers do their tenant-scoped work inside the returned scope. With a
    /// <paramref name="member"/>, the matter-bound tables also show only that member's
    /// matters (<c>app.current_user</c>, <c>app.all_matters</c>, read by
    /// <c>korkut.can_see_matter</c>); without one (a background job) only the office boundary applies.
    /// </summary>
    public static async Task<ITenantScope> BeginTenantScopeAsync(
        this DbContext db, Guid officeId, MatterScopeMember? member = null, CancellationToken cancellationToken = default)
    {
        var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        // is_local = true → scoped to this transaction only.
        var user = member?.UserId.ToString() ?? string.Empty;
        var allMatters = member is { AllMatters: false } ? "0" : "1";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config({SettingName}, {officeId.ToString()}, true), set_config('app.current_user', {user}, true), set_config('app.all_matters', {allMatters}, true)",
            cancellationToken);
        return new TenantScope(tx);
    }

    /// <summary>Begin a tenant transaction with no member behind it (office boundary only).</summary>
    public static Task<ITenantScope> BeginTenantScopeAsync(this DbContext db, Guid officeId, CancellationToken cancellationToken)
        => db.BeginTenantScopeAsync(officeId, null, cancellationToken);

    /// <summary>
    /// The member a scope runs as, for the matter-bound RLS policies; null when nobody is behind it
    /// (a job, a request before sign-in) or the caller belongs to another office.
    /// </summary>
    public static MatterScopeMember? MemberOf(Korkut.Core.ICurrentUser? user, Guid officeId)
        => user is { IsKnown: true } && user.OfficeId == officeId
            ? new MatterScopeMember(user.UserId, user.Permissions.Contains(Korkut.Core.Permissions.MattersViewAll))
            : null;

    private sealed class TenantScope(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) : ITenantScope
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default) =>
            await tx.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync() => await tx.DisposeAsync();
    }
}

/// <summary>Who a tenant scope runs as, for the matter-bound tables.</summary>
public sealed record MatterScopeMember(Guid UserId, bool AllMatters);

/// <summary>A tenant-pinned transaction scope.</summary>
public interface ITenantScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
