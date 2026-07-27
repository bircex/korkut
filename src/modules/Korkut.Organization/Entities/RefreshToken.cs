using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// An opaque, hashed refresh token in a rotating family. Each token is used once; presenting a
/// token that was already used or revoked is treated as theft and takes the whole family down.
/// </summary>
/// <remarks>
/// The transitions (rotate one token, revoke a family) are NOT methods here: they must be atomic
/// against a replay racing the same token, so they live in SECURITY DEFINER SQL functions —
/// exactly like the outbox lease. This type is the shape those functions read and write; only the
/// hash is ever stored, never the raw token.
/// </remarks>
public sealed class RefreshToken : Entity, ITenantOwned, IAggregateRoot
{
    public RefreshToken(
        Guid id, Guid familyId, Guid officeId, Guid userId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset createdAt)
        : base(id)
    {
        FamilyId = familyId;
        OfficeId = officeId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private RefreshToken()
    {
    }

    /// <summary>All tokens rotated from one sign-in share a family id.</summary>
    public Guid FamilyId { get; private set; }

    public Guid OfficeId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the opaque token; the raw value is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set when this token is rotated (consumed). A second use is a reuse.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Set when the token, or its family, is revoked.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
