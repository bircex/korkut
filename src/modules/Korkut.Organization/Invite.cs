using Korkut.Core;

namespace Korkut.Organization;

/// <summary>
/// An invitation to join an office. Only the hash of the opaque token is kept, so a
/// leaked database cannot be used to accept anything; the raw token exists once, in the
/// response that created the invite. Single-use: accepting stamps it and it stops being
/// pending.
/// </summary>
public sealed class Invite : Entity, ITenantOwned, IAggregateRoot
{
    public Invite(
        Guid officeId, string email, string role, string tokenHash,
        DateTimeOffset expiresAt, Guid? createdBy, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Invite()
    {
    }

    public Guid OfficeId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    /// <summary>"owner" | "lawyer".</summary>
    public string Role { get; private set; } = "lawyer";

    /// <summary>SHA-256 of the opaque invite token; the raw token is never stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>The owner who sent it.</summary>
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Still open: nobody has accepted it yet.</summary>
    public bool IsPending => AcceptedAt is null;

    /// <summary>Consumes the invite; an invite already accepted cannot be accepted again.</summary>
    public bool Accept(DateTimeOffset acceptedAt)
    {
        if (!IsPending)
        {
            return false;
        }

        AcceptedAt = acceptedAt;
        return true;
    }
}
