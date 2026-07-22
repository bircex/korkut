using Korkut.Application.Auditing;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Security;

public enum TwoFactorResult
{
    Success,
    InvalidCode,
    NotEnrolled,
    Replay,
}

/// <summary>Result of beginning enrollment: the provisioning URI to show once.</summary>
public sealed record EnrollmentStart(string ProvisioningUri);

/// <summary>
/// Office-policy TOTP: enrollment, challenge (clock-window + replay guard), hashed
/// single-use recovery codes, and audited, session-revoking reset. Secrets are
/// envelope-encrypted; only hashes/ciphertext are persisted.
/// </summary>
public sealed class TwoFactorService(
    KorkutDbContext db,
    ISecretEncryptor encryptor,
    ITokenHasher tokenHasher,
    IAuditLog audit,
    IClock clock)
{
    public int RecoveryCodeCount { get; init; } = 10;

    public async Task<EnrollmentStart> BeginEnrollmentAsync(Guid officeId, Guid userId, string account, CancellationToken ct = default)
    {
        var secret = Totp.NewSecret();
        var existing = await db.UserTotps.FirstOrDefaultAsync(x => x.OfficeId == officeId && x.UserId == userId, ct);
        if (existing is null)
        {
            db.UserTotps.Add(new UserTotpEntity
            {
                Id = UuidV7.NewId(), OfficeId = officeId, UserId = userId,
                EncryptedSecret = encryptor.Encrypt(secret), Enabled = false, CreatedAt = clock.UtcNow,
            });
        }
        else if (!existing.Enabled)
        {
            existing.EncryptedSecret = encryptor.Encrypt(secret); // re-roll pending secret
        }
        else
        {
            throw new InvalidOperationException("TOTP already enrolled; reset first.");
        }

        await db.SaveChangesAsync(ct);
        return new EnrollmentStart(Totp.ProvisioningUri(secret, "Korkut", account));
    }

    /// <summary>Confirm enrollment with a live code; returns the one-time recovery codes.</summary>
    public async Task<(TwoFactorResult Result, IReadOnlyList<string> RecoveryCodes)> ConfirmEnrollmentAsync(
        Guid officeId, Guid userId, string code, CancellationToken ct = default)
    {
        var totp = await db.UserTotps.FirstOrDefaultAsync(x => x.OfficeId == officeId && x.UserId == userId, ct);
        if (totp is null)
        {
            return (TwoFactorResult.NotEnrolled, []);
        }

        var step = Totp.Verify(encryptor.Decrypt(totp.EncryptedSecret), code, clock.UtcNow);
        if (step is null)
        {
            return (TwoFactorResult.InvalidCode, []);
        }

        totp.Enabled = true;
        totp.ConfirmedAt = clock.UtcNow;
        totp.LastUsedStep = step.Value;

        var raw = new List<string>();
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var codeRaw = tokenHasher.NewRawToken();
            raw.Add(codeRaw);
            db.RecoveryCodes.Add(new RecoveryCodeEntity
            {
                Id = UuidV7.NewId(), OfficeId = officeId, UserId = userId,
                CodeHash = tokenHasher.Hash(codeRaw), CreatedAt = clock.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        // Mark the user 2FA-enabled so the login gate (KDM-016) requires a challenge.
        await db.Users.Where(x => x.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorEnabled, true), ct);
        await audit.WriteAsync(new AuditEntry(officeId, userId, null, null, "2fa.enrolled", "user", userId, null, null), ct);
        return (TwoFactorResult.Success, raw);
    }

    public async Task<TwoFactorResult> ChallengeAsync(Guid officeId, Guid userId, string code, CancellationToken ct = default)
    {
        var totp = await db.UserTotps.FirstOrDefaultAsync(x => x.OfficeId == officeId && x.UserId == userId && x.Enabled, ct);
        if (totp is null)
        {
            return TwoFactorResult.NotEnrolled;
        }

        var step = Totp.Verify(encryptor.Decrypt(totp.EncryptedSecret), code, clock.UtcNow);
        if (step is null)
        {
            return TwoFactorResult.InvalidCode;
        }

        // Replay guard: a code (step) already accepted cannot be used again.
        if (step.Value <= totp.LastUsedStep)
        {
            return TwoFactorResult.Replay;
        }

        totp.LastUsedStep = step.Value;
        await db.SaveChangesAsync(ct);
        return TwoFactorResult.Success;
    }

    public async Task<bool> UseRecoveryCodeAsync(Guid officeId, Guid userId, string rawCode, CancellationToken ct = default)
    {
        var hash = tokenHasher.Hash(rawCode);
        // Atomic single-use: only the first use flips used_at.
        var affected = await db.RecoveryCodes
            .Where(x => x.OfficeId == officeId && x.UserId == userId && x.CodeHash == hash && x.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, clock.UtcNow), ct);
        if (affected == 0)
        {
            return false;
        }

        await audit.WriteAsync(new AuditEntry(officeId, userId, null, null, "2fa.recovery_used", "user", userId, null, null), ct);
        return true;
    }

    /// <summary>
    /// Reset a user's 2FA. Requires an out-of-band high-secure approval and revokes
    /// the user's sessions (security stamp + token epoch bump). Audited.
    /// </summary>
    public async Task ResetAsync(Guid officeId, Guid userId, bool approved, CancellationToken ct = default)
    {
        if (!approved)
        {
            throw new InvalidOperationException("2FA reset requires high-secure approval.");
        }

        await db.UserTotps.Where(x => x.OfficeId == officeId && x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.RecoveryCodes.Where(x => x.OfficeId == officeId && x.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(
            s => s.SetProperty(x => x.TwoFactorEnabled, false)
                  .SetProperty(x => x.SecurityStamp, Guid.CreateVersion7())
                  .SetProperty(x => x.TokenEpoch, x => x.TokenEpoch + 1), ct);
        await audit.WriteAsync(new AuditEntry(officeId, userId, null, null, "2fa.reset", "user", userId, null, null), ct);
    }
}
