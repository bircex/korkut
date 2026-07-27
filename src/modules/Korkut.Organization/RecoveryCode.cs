using Korkut.Core;

namespace Korkut.Organization;

/// <summary>
/// A single-use, hashed TOTP recovery code — the way back in when the authenticator is lost. Only
/// the hash is stored. Spending it is an atomic conditional update (so a replay loses the race),
/// which is why consumption is not a method here.
/// </summary>
public sealed class RecoveryCode : Entity, ITenantOwned, IAggregateRoot
{
    public RecoveryCode(Guid officeId, Guid userId, string codeHash, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        UserId = userId;
        CodeHash = codeHash;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private RecoveryCode()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
