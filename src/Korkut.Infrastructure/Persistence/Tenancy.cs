using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Transaction-scoped tenant context. RLS policies read
/// <c>current_setting('app.current_office')</c>; this sets it with
/// <c>set_config(..., is_local := true)</c> so it is bound to the current
/// transaction and can never leak onto the next borrower of a pooled connection.
/// The tenant is established from the trusted session (KDM-018), never from a DTO.
/// </summary>
public static class Tenancy
{
    public const string SettingName = "app.current_office";

    /// <summary>
    /// Begin a transaction and pin the tenant for its duration. Dispose commits;
    /// callers do their tenant-scoped work inside the returned scope.
    /// </summary>
    public static async Task<ITenantScope> BeginTenantScopeAsync(
        this DbContext db, Guid officeId, CancellationToken cancellationToken = default)
    {
        var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        // is_local = true → scoped to this transaction only.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config({SettingName}, {officeId.ToString()}, true)", cancellationToken);
        return new TenantScope(tx);
    }

    private sealed class TenantScope(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) : ITenantScope
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default) =>
            await tx.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync() => await tx.DisposeAsync();
    }
}

/// <summary>A tenant-pinned transaction scope.</summary>
public interface ITenantScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
