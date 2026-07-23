using Korkut.Domain.Common;
namespace Korkut.Infrastructure.Persistence.Entities;

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
