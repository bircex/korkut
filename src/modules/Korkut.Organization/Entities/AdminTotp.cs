using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// Platform-admin TOTP enrollment, keyed by the ALLOWLISTED e-mail (admins are not office
/// users and have no tenant). Mirrors <see cref="UserTotp"/>'s lifecycle: a pending seed is
/// re-rollable until the first code confirms enrollment; afterwards the accepted-step
/// watermark guards replay. Cross-tenant by design — no RLS, granted to the app role by the
/// AddAdminConsole migration.
/// </summary>
public sealed class AdminTotp : Entity, IAggregateRoot
{
    public AdminTotp(string email, string encryptedSecret, DateTimeOffset createdAt)
        : base(NewId())
    {
        Email = email;
        EncryptedSecret = encryptedSecret;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private AdminTotp()
    {
    }

    /// <summary>Normalized (trim + lower) allowlist e-mail; unique.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Envelope-encrypted TOTP seed (never stored or logged in plaintext).</summary>
    public string EncryptedSecret { get; private set; } = string.Empty;

    /// <summary>False until the first code is confirmed during enrollment.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Highest TOTP step already accepted — the replay guard.</summary>
    public long LastUsedStep { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Re-rolls the pending seed while still enrolling. Refused once enrollment is confirmed.</summary>
    public void ReplacePendingSecret(string encryptedSecret)
    {
        if (Enabled)
        {
            throw new InvalidOperationException("Admin TOTP already enrolled.");
        }

        EncryptedSecret = encryptedSecret;
    }

    public void Confirm(long step, DateTimeOffset now)
    {
        Enabled = true;
        ConfirmedAt = now;
        LastUsedStep = step;
    }

    public bool IsReplay(long step) => step <= LastUsedStep;

    public void RecordAcceptedStep(long step) => LastUsedStep = step;
}
