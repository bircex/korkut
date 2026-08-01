using Korkut.Core;
using Korkut.Organization.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Security;

/// <summary>
/// Platform-admin TOTP over the cross-tenant admin tables — the admin mirror of
/// <see cref="TwoFactorService"/>, keyed by the ALLOWLISTED e-mail instead of a tenant user:
/// enrollment (pending seed re-rollable until the first code confirms), challenge with a
/// clock-window + accepted-step replay guard, and hashed single-use recovery codes. Also the
/// <see cref="IAdminEnrollment"/> port the Google callback consults to route an allowlisted
/// admin to challenge vs bootstrap. No RLS is involved (admins have no tenant); secrets are
/// envelope-encrypted, only hashes/ciphertext persist.
/// </summary>
public sealed class AdminTwoFactorService(
    DbContext db,
    ISecretEncryptor encryptor,
    ITokenHasher tokenHasher,
    IClock clock) : IAdminEnrollment
{
    public int RecoveryCodeCount { get; init; } = 10;

    public async Task<bool> IsEnrolledAsync(string normalizedEmail, CancellationToken ct)
        => await db.Set<AdminTotp>().AnyAsync(x => x.Email == normalizedEmail && x.Enabled, ct);

    /// <summary>Begin (or re-roll a pending) enrollment; returns the provisioning URI + base32 secret.</summary>
    public async Task<(string ProvisioningUri, string Secret)> BeginEnrollmentAsync(string email, CancellationToken ct = default)
    {
        var secret = Totp.NewSecret();
        var existing = await db.Set<AdminTotp>().FirstOrDefaultAsync(x => x.Email == email, ct);
        if (existing is null)
        {
            db.Set<AdminTotp>().Add(new AdminTotp(email, encryptor.Encrypt(secret), clock.UtcNow));
        }
        else
        {
            existing.ReplacePendingSecret(encryptor.Encrypt(secret)); // refused once enrolled
        }

        await db.SaveChangesAsync(ct);
        return (Totp.ProvisioningUri(secret, "Korkut Admin", email), Totp.Base32Secret(secret));
    }

    /// <summary>Confirm enrollment with a live code; returns the one-time recovery codes.</summary>
    public async Task<(TwoFactorResult Result, IReadOnlyList<string> RecoveryCodes)> ConfirmEnrollmentAsync(
        string email, string code, CancellationToken ct = default)
    {
        var totp = await db.Set<AdminTotp>().FirstOrDefaultAsync(x => x.Email == email, ct);
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
            db.Set<AdminRecoveryCode>().Add(new AdminRecoveryCode(email, tokenHasher.Hash(codeRaw), clock.UtcNow));
        }

        await db.SaveChangesAsync(ct);
        return (TwoFactorResult.Success, raw);
    }

    public async Task<TwoFactorResult> ChallengeAsync(string email, string code, CancellationToken ct = default)
    {
        var totp = await db.Set<AdminTotp>().FirstOrDefaultAsync(x => x.Email == email && x.Enabled, ct);
        if (totp is null)
        {
            return TwoFactorResult.NotEnrolled;
        }

        var step = Totp.Verify(encryptor.Decrypt(totp.EncryptedSecret), code, clock.UtcNow);
        if (step is null)
        {
            return TwoFactorResult.InvalidCode;
        }

        if (totp.IsReplay(step.Value))
        {
            return TwoFactorResult.Replay;
        }

        totp.RecordAcceptedStep(step.Value);
        await db.SaveChangesAsync(ct);
        return TwoFactorResult.Success;
    }

    /// <summary>Atomic single-use recovery: only the first use flips used_at.</summary>
    public async Task<bool> UseRecoveryCodeAsync(string email, string rawCode, CancellationToken ct = default)
    {
        var hash = tokenHasher.Hash(rawCode);
        var affected = await db.Set<AdminRecoveryCode>()
            .Where(x => x.Email == email && x.CodeHash == hash && x.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, clock.UtcNow), ct);
        return affected > 0;
    }
}
