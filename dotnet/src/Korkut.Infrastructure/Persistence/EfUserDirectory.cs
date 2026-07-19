using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>EF-backed <see cref="IUserDirectory"/> over PostgreSQL.</summary>
public sealed class EfUserDirectory(KorkutDbContext db) : IUserDirectory
{
    public async Task<AuthUser?> FindByEmailAsync(Guid officeId, string normalizedEmail, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfficeId == officeId && x.Email == normalizedEmail, ct);
        return Map(u);
    }

    public async Task<AuthUser?> FindByIdAsync(Guid officeId, Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfficeId == officeId && x.Id == userId, ct);
        return Map(u);
    }

    public async Task RecordFailedAttemptAsync(Guid userId, int failedCount, DateTimeOffset? lockoutEnd, CancellationToken ct)
    {
        await db.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(
            s => s.SetProperty(x => x.FailedAccessCount, failedCount).SetProperty(x => x.LockoutEnd, lockoutEnd), ct);
    }

    public async Task ResetFailedAttemptsAsync(Guid userId, CancellationToken ct)
    {
        await db.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(
            s => s.SetProperty(x => x.FailedAccessCount, 0).SetProperty(x => x.LockoutEnd, (DateTimeOffset?)null), ct);
    }

    public async Task SetPasswordAsync(Guid userId, string passwordHash, Guid newSecurityStamp, int newTokenEpoch, CancellationToken ct)
    {
        await db.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(
            s => s.SetProperty(x => x.PasswordHash, passwordHash)
                  .SetProperty(x => x.SecurityStamp, newSecurityStamp)
                  .SetProperty(x => x.TokenEpoch, newTokenEpoch)
                  .SetProperty(x => x.FailedAccessCount, 0)
                  .SetProperty(x => x.LockoutEnd, (DateTimeOffset?)null), ct);
    }

    public async Task CreateResetTokenAsync(Guid userId, Guid officeId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken ct)
    {
        db.PasswordResetTokens.Add(new PasswordResetTokenEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ResetTokenRecord?> FindActiveResetTokenAsync(Guid officeId, string tokenHash, DateTimeOffset now, CancellationToken ct)
    {
        var t = await db.PasswordResetTokens.AsNoTracking().FirstOrDefaultAsync(
            x => x.OfficeId == officeId && x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > now, ct);
        return t is null ? null : new ResetTokenRecord(t.Id, t.UserId, t.OfficeId);
    }

    public async Task<bool> TryConsumeResetTokenAsync(Guid tokenId, DateTimeOffset now, CancellationToken ct)
    {
        // Atomic single-use: only the first caller flips UsedAt from null; concurrent
        // replays update 0 rows.
        var affected = await db.PasswordResetTokens
            .Where(x => x.Id == tokenId && x.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, now), ct);
        return affected == 1;
    }

    private static AuthUser? Map(UserEntity? u) => u is null
        ? null
        : new AuthUser(u.Id, u.OfficeId, u.Email, u.PasswordHash, u.FailedAccessCount, u.LockoutEnd, u.SecurityStamp, u.TokenEpoch);
}

/// <summary>Structured-log security notifier (no secrets/tokens). A real channel is wired in KDM-024.</summary>
public sealed partial class LoggingSecurityNotifier(Microsoft.Extensions.Logging.ILogger<LoggingSecurityNotifier> logger)
    : ISecurityNotifier
{
    public Task NotifyPasswordChangedAsync(Guid officeId, string email, CancellationToken ct)
    {
        LogPasswordChanged(logger, officeId);
        return Task.CompletedTask;
    }

    [Microsoft.Extensions.Logging.LoggerMessage(
        Level = Microsoft.Extensions.Logging.LogLevel.Information,
        Message = "Password changed for a user in office {OfficeId}")]
    private static partial void LogPasswordChanged(Microsoft.Extensions.Logging.ILogger logger, Guid officeId);
}
