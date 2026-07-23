using Korkut.Domain.Common;

namespace Korkut.Domain.Users;

/// <summary>
/// A person with access to an office. One aggregate carries both sides of that: who they
/// are to the office (e-mail, name, bar registry number, role) and how they authenticate
/// (password hash, Google link, TOTP, lockout counters).
/// </summary>
/// <remarks>
/// Every change that alters what a session is allowed to do rotates <see cref="SecurityStamp"/>,
/// which is what makes existing sessions stop being trusted; the aggregate does that itself
/// so no caller can change a role and leave the old sessions alive.
/// </remarks>
public sealed class User : Entity, ITenantOwned, IAggregateRoot
{
    public User(Guid officeId, string email, string role, string? fullName, string? barRegistryNo, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Email = email;
        Role = role;
        FullName = fullName;
        BarRegistryNo = barRegistryNo;
        SecurityStamp = NewId();
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private User()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>Normalized (trimmed, lower-cased) e-mail — unique per office.</summary>
    public string Email { get; private set; } = string.Empty;

    public string? FullName { get; private set; }

    /// <summary>Bar registry number ("baro sicil no").</summary>
    public string? BarRegistryNo { get; private set; }

    /// <summary>Versioned PBKDF2 hash; null until a local password is set.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Role key ('owner' | 'lawyer' | a custom role key) — a named permission set.</summary>
    public string Role { get; private set; } = "lawyer";

    /// <summary>Google provider subject — the stable identity link (never the e-mail).</summary>
    public string? GoogleSub { get; private set; }

    /// <summary>Whether the user has Korkut TOTP enrolled.</summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>Rotated on any credential/authorization change to invalidate stale sessions.</summary>
    public Guid SecurityStamp { get; private set; }

    /// <summary>Monotonic revocation counter carried in the session.</summary>
    public int TokenEpoch { get; private set; }

    public int FailedAccessCount { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The person has completed a first Google sign-in, so their account is linked.</summary>
    public bool IsBound => GoogleSub is not null;

    /// <summary>False while the account is locked out.</summary>
    public bool IsActive => LockoutEnd is null;

    /// <summary>Updates the fields a person may change about themselves.</summary>
    public void UpdateProfile(string? fullName, string? barRegistryNo)
    {
        FullName = fullName;
        BarRegistryNo = barRegistryNo;
    }

    /// <summary>Changes the role; the rotated stamp revokes sessions carrying the old one.</summary>
    public void ChangeRole(string role)
    {
        Role = role;
        SecurityStamp = NewId();
    }

    /// <summary>Sets the local password, revoking existing sessions and clearing any lockout.</summary>
    public void SetPassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        SecurityStamp = NewId();
        TokenEpoch++;
        FailedAccessCount = 0;
        LockoutEnd = null;
    }

    /// <summary>Links the Google identity that will sign this person in from now on.</summary>
    public void LinkGoogle(string googleSub) => GoogleSub = googleSub;

    public void EnableTwoFactor() => TwoFactorEnabled = true;

    public void DisableTwoFactor() => TwoFactorEnabled = false;
}
