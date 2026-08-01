using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>Hashed single-use recovery code for a platform admin (keyed by allowlist e-mail).</summary>
public sealed class AdminRecoveryCode : Entity, IAggregateRoot
{
    public AdminRecoveryCode(string email, string codeHash, DateTimeOffset createdAt)
        : base(NewId())
    {
        Email = email;
        CodeHash = codeHash;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private AdminRecoveryCode()
    {
    }

    public string Email { get; private set; } = string.Empty;

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }
}
