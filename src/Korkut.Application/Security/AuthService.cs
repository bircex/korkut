using Korkut.Domain.Time;

namespace Korkut.Application.Security;

/// <summary>A user as the auth flows need it (no navigation/persistence concerns).</summary>
public sealed record AuthUser(
    Guid Id, Guid OfficeId, string Email, string? PasswordHash,
    int FailedAccessCount, DateTimeOffset? LockoutEnd, Guid SecurityStamp, int TokenEpoch);

/// <summary>An active (unexpired, unused) reset token row.</summary>
public sealed record ResetTokenRecord(Guid Id, Guid UserId, Guid OfficeId);

public enum AuthOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
}

public enum ResetOutcome
{
    Success,
    InvalidOrExpiredToken,
}

/// <summary>Persistence boundary for the auth flows (implemented over EF in Infrastructure).</summary>
public interface IUserDirectory
{
    Task<AuthUser?> FindByEmailAsync(Guid officeId, string normalizedEmail, CancellationToken ct);

    Task<AuthUser?> FindByIdAsync(Guid officeId, Guid userId, CancellationToken ct);

    Task RecordFailedAttemptAsync(Guid userId, int failedCount, DateTimeOffset? lockoutEnd, CancellationToken ct);

    Task ResetFailedAttemptsAsync(Guid userId, CancellationToken ct);

    Task SetPasswordAsync(Guid userId, string passwordHash, Guid newSecurityStamp, int newTokenEpoch, CancellationToken ct);

    Task CreateResetTokenAsync(Guid userId, Guid officeId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now, CancellationToken ct);

    Task<ResetTokenRecord?> FindActiveResetTokenAsync(Guid officeId, string tokenHash, DateTimeOffset now, CancellationToken ct);

    /// <summary>Atomically mark the token used; returns false if it was already consumed (replay/race).</summary>
    Task<bool> TryConsumeResetTokenAsync(Guid tokenId, DateTimeOffset now, CancellationToken ct);
}

/// <summary>Security notifications (password changed). Never includes secrets/tokens.</summary>
public interface ISecurityNotifier
{
    Task NotifyPasswordChangedAsync(Guid officeId, string email, CancellationToken ct);
}

/// <summary>Hashes opaque reset tokens for at-rest storage (never store the raw token).</summary>
public interface ITokenHasher
{
    string Hash(string rawToken);

    string NewRawToken();
}

/// <summary>
/// Local authentication, lockout and password recovery. Login is enumeration-safe
/// (a missing user still incurs a hash verification so responses/timing don't
/// reveal existence); reset requests never reveal whether an account exists; reset
/// tokens are single-use, short-lived and stored hashed; and any credential change
/// bumps the security stamp + token epoch to revoke existing sessions.
/// </summary>
public sealed class AuthService(
    IUserDirectory users,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    ISecurityNotifier notifier,
    IClock clock)
{
    public int MaxFailedAttempts { get; init; } = 5;

    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan ResetTokenLifetime { get; init; } = TimeSpan.FromHours(1);

    // A fixed dummy hash so verification always runs, even for an unknown user.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.Hash("timing-only-not-a-real-password"));

    public async Task<AuthOutcome> AuthenticateAsync(Guid officeId, string email, string password, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(officeId, Normalize(email), ct);
        var now = clock.UtcNow;

        if (user is not null && user.LockoutEnd is { } until && until > now)
        {
            return AuthOutcome.LockedOut;
        }

        // Always verify against *some* hash to avoid revealing that the account is missing.
        var hash = user?.PasswordHash ?? _dummyHash.Value;
        var (verified, needsRehash) = passwordHasher.Verify(password, hash);

        if (user is null || !verified)
        {
            if (user is not null)
            {
                await RegisterFailureAsync(user, now, ct);
            }

            return AuthOutcome.InvalidCredentials;
        }

        await users.ResetFailedAttemptsAsync(user.Id, ct);
        if (needsRehash)
        {
            await users.SetPasswordAsync(user.Id, passwordHasher.Hash(password), user.SecurityStamp, user.TokenEpoch, ct);
        }

        return AuthOutcome.Success;
    }

    /// <summary>Always succeeds from the caller's view — it must not reveal account existence.</summary>
    public async Task RequestPasswordResetAsync(Guid officeId, string email, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(officeId, Normalize(email), ct);
        if (user is null)
        {
            return; // silently no-op: no enumeration signal
        }

        var raw = tokenHasher.NewRawToken();
        var now = clock.UtcNow;
        await users.CreateResetTokenAsync(user.Id, officeId, tokenHasher.Hash(raw), now + ResetTokenLifetime, now, ct);
        // The raw token is delivered out of band (email); it is never logged/audited.
    }

    public async Task<ResetOutcome> ResetPasswordAsync(Guid officeId, string rawToken, string newPassword, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var token = await users.FindActiveResetTokenAsync(officeId, tokenHasher.Hash(rawToken), now, ct);
        if (token is null)
        {
            return ResetOutcome.InvalidOrExpiredToken;
        }

        // Single-use: consuming is atomic, so a replay or concurrent reset loses the race.
        if (!await users.TryConsumeResetTokenAsync(token.Id, now, ct))
        {
            return ResetOutcome.InvalidOrExpiredToken;
        }

        var user = await users.FindByIdAsync(officeId, token.UserId, ct);
        if (user is null)
        {
            return ResetOutcome.InvalidOrExpiredToken;
        }

        // Rotate security stamp + token epoch → existing sessions are revoked.
        await users.SetPasswordAsync(user.Id, passwordHasher.Hash(newPassword), Guid.CreateVersion7(), user.TokenEpoch + 1, ct);
        await notifier.NotifyPasswordChangedAsync(officeId, user.Email, ct);
        return ResetOutcome.Success;
    }

    private async Task RegisterFailureAsync(AuthUser user, DateTimeOffset now, CancellationToken ct)
    {
        var failed = user.FailedAccessCount + 1;
        DateTimeOffset? lockout = failed >= MaxFailedAttempts ? now + LockoutDuration : null;
        await users.RecordFailedAttemptAsync(user.Id, failed, lockout, ct);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
