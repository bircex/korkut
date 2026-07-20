namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A pending office invitation. The raw token is emailed to the invitee and stored
/// only as a hash; accepting it creates the member (email + role) so the invitee can
/// then sign in (Google/local) into that office. Single-use, short-lived, tenant-owned.
/// </summary>
public sealed class InviteEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>"owner" | "lawyer".</summary>
    public string Role { get; set; } = "lawyer";

    /// <summary>SHA-256 of the opaque invite token; the raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
