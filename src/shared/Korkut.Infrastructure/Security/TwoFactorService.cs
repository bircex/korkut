using Korkut.Core;
using Korkut.Infrastructure.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Organization;
using Korkut.Organization.Entities;
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
    IClock clock) : ITwoFactorChallenge
{
    public int RecoveryCodeCount { get; init; } = 10;

    public async Task<EnrollmentStart> BeginEnrollmentAsync(Guid officeId, Guid userId, string account, CancellationToken ct = default)
    {
        var secret = Totp.NewSecret();
        var existing = await db.UserTotps.FirstOrDefaultAsync(x => x.OfficeId == officeId && x.UserId == userId, ct);
        if (existing is null)
        {
            db.UserTotps.Add(new UserTotp(officeId, userId, encryptor.Encrypt(secret), clock.UtcNow));
        }
        else
        {
            existing.ReplacePendingSecret(encryptor.Encrypt(secret)); // re-roll; refused once enrolled
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

        totp.Confirm(step.Value, clock.UtcNow);

        var raw = new List<string>();
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var codeRaw = tokenHasher.NewRawToken();
            raw.Add(codeRaw);
            db.RecoveryCodes.Add(new RecoveryCode(officeId, userId, tokenHasher.Hash(codeRaw), clock.UtcNow));
        }

        await db.SaveChangesAsync(ct);
        // Mark the user 2FA-enabled so the login gate (KDM-016) requires a challenge.
        await db.Users.Where(x => x.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorEnabled, true), ct);
        await audit.WriteAsync(new AuditEntry(officeId, userId, null, null, "2fa.enrolled", "user", userId, null, null), ct);
        return (TwoFactorResult.Success, raw);
    }

    /// <summary>The sign-in port: a code is either valid right now, or it is not.</summary>
    public async Task<bool> VerifyCodeAsync(Guid officeId, Guid userId, string code, CancellationToken ct = default)
        => await ChallengeAsync(officeId, userId, code, ct) == TwoFactorResult.Success;

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
        if (totp.IsReplay(step.Value))
        {
            return TwoFactorResult.Replay;
        }

        totp.RecordAcceptedStep(step.Value);
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
