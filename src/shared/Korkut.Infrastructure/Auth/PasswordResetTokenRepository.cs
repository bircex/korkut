using Korkut.Organization;
using Korkut.Persistence;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Auth;

/// <summary>EF/Postgres adapter for <see cref="IPasswordResetTokenRepository"/>.</summary>
public sealed class PasswordResetTokenRepository(KorkutDbContext db)
    : Repository<PasswordResetToken>(db), IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindUsableAsync(
        Guid officeId, string tokenHash, DateTimeOffset now, CancellationToken ct = default)
        => Scoped(officeId).AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > now, ct);

    /// <summary>
    /// One conditional UPDATE: only the first caller flips UsedAt from null, so a replay or
    /// a concurrent reset updates zero rows and is told the token is spent.
    /// </summary>
    public async Task<bool> TryConsumeAsync(
        Guid officeId, Guid tokenId, DateTimeOffset usedAt, CancellationToken ct = default)
        => await Scoped(officeId).Where(t => t.Id == tokenId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, usedAt), ct) == 1;
}
