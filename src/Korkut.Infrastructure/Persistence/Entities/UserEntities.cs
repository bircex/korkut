namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>A tenant-owned office user with local credentials (KDM-015).</summary>
public sealed class UserEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>Normalized (trimmed, lower-cased) email — unique per office.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name (Python parity: the person's full name).</summary>
    public string? FullName { get; set; }

    /// <summary>Bar registry number ("baro sicil no") — Python parity.</summary>
    public string? BarRegistryNo { get; set; }

    /// <summary>Versioned PBKDF2 hash; null until a local password is set.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Role key ('owner' | 'lawyer' | a custom role key) — a named permission set.</summary>
    public string Role { get; set; } = "lawyer";

    /// <summary>Google provider subject — the stable identity link (never the email).</summary>
    public string? GoogleSub { get; set; }

    /// <summary>Whether the user has Korkut TOTP enrolled (KDM-017).</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Rotated on any credential/authorization change to invalidate stale sessions.</summary>
    public Guid SecurityStamp { get; set; }

    /// <summary>Monotonic revocation counter carried in the session (KDM-018).</summary>
    public int TokenEpoch { get; set; }

    public int FailedAccessCount { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A single-use, short-lived, hashed password-reset token.</summary>
public sealed class PasswordResetTokenEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the opaque token; the raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
