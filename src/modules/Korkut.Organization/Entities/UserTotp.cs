using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// A user's TOTP enrollment. The seed is envelope-encrypted at rest and never leaves the
/// encryptor. Enrollment is two-step: the row starts disabled and only becomes usable once the
/// user proves a live code. A code is single-use per time step, so an accepted step can never be
/// replayed.
/// </summary>
public sealed class UserTotp : Entity, ITenantOwned, IAggregateRoot
{
    public UserTotp(Guid officeId, Guid userId, string encryptedSecret, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        UserId = userId;
        EncryptedSecret = encryptedSecret;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private UserTotp()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid UserId { get; private set; }

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
            throw new InvalidOperationException("TOTP already enrolled; reset first.");
        }

        EncryptedSecret = encryptedSecret;
    }

    /// <summary>Confirms enrollment with the step of the live code that proved possession of the seed.</summary>
    public void Confirm(long step, DateTimeOffset confirmedAt)
    {
        Enabled = true;
        ConfirmedAt = confirmedAt;
        LastUsedStep = step;
    }

    /// <summary>True when this step was already accepted — the same code cannot be used twice.</summary>
    public bool IsReplay(long step) => step <= LastUsedStep;

    /// <summary>Records a freshly accepted step so it cannot be replayed.</summary>
    public void RecordAcceptedStep(long step) => LastUsedStep = step;
}
