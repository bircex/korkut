namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// An opaque, hashed refresh token in a rotating family. Each token is used once;
/// presenting an already-used/revoked token is treated as theft and revokes the
/// entire family (KDM-018).
/// </summary>
public sealed class RefreshTokenEntity : ITenantOwned
{
    public Guid Id { get; set; }

    /// <summary>All tokens rotated from one login share a family id.</summary>
    public Guid FamilyId { get; set; }

    public Guid OfficeId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the opaque token; the raw value is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when this token is rotated (consumed). A second use is a reuse.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>Set when the token (or its family) is revoked.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
