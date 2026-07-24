using Korkut.Core;

namespace Korkut.Domain.Offices;

/// <summary>
/// A law office: the tenant root. Every other aggregate is owned by one of these, which is
/// why this one is NOT <see cref="ITenantOwned"/> and its table is not RLS-forced — it is
/// the thing tenancy is defined by, managed by platform admins rather than by an office.
/// </summary>
public sealed class Office : Entity, IAggregateRoot
{
    public Office(string name, DateTimeOffset createdAt)
        : this(NewId(), name, createdAt)
    {
    }

    /// <summary>
    /// Rehydrates an office whose id was assigned elsewhere. Self-serve signup provisions
    /// the row (and its id) inside the database, through the SECURITY DEFINER function, and
    /// a data migration carries existing ids over; both need to name the id.
    /// </summary>
    public Office(Guid id, string name, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Office()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>A disabled office cannot sign in (demo-approval / suspension gate).</summary>
    public bool Disabled { get; private set; }

    /// <summary>When true, every office user must complete Korkut TOTP at login.</summary>
    public bool RequireTwoFactor { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Lets the office sign in again.</summary>
    public void Enable() => Disabled = false;

    /// <summary>Suspends the office; nobody in it can sign in until it is enabled again.</summary>
    public void Disable() => Disabled = true;

    public void SetTwoFactorRequirement(bool required) => RequireTwoFactor = required;
}
