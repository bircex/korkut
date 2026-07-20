namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>A user's TOTP enrollment. Secret is envelope-encrypted at rest.</summary>
public sealed class UserTotpEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Envelope-encrypted TOTP seed (never stored/logged in plaintext).</summary>
    public string EncryptedSecret { get; set; } = string.Empty;

    /// <summary>False until the first code is confirmed during enrollment.</summary>
    public bool Enabled { get; set; }

    /// <summary>Highest TOTP step already accepted — rejects replay of the same code.</summary>
    public long LastUsedStep { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
}

/// <summary>A single-use, hashed TOTP recovery code.</summary>
public sealed class RecoveryCodeEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
