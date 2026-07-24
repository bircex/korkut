using Korkut.Core;

namespace Korkut.Domain.Auth;

/// <summary>
/// A short-lived, single-use ticket to set a new password without knowing the old one.
/// Only the hash is stored, so the database alone cannot reset anyone's password; the raw
/// token exists in the e-mail that was sent and nowhere else.
/// </summary>
/// <remarks>
/// Consumption is deliberately NOT a method here: it must be atomic against a concurrent
/// replay, so the repository does it as one conditional update and this type only says
/// whether a token looks usable (see <c>IPasswordResetTokenRepository.TryConsumeAsync</c>).
/// </remarks>
public sealed class PasswordResetToken : Entity, ITenantOwned, IAggregateRoot
{
    public PasswordResetToken(
        Guid officeId, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private PasswordResetToken()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the opaque token; the raw token is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Unused and not yet expired.</summary>
    public bool IsUsable(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;
}
