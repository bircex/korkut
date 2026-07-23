
namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>The tenant root (an office/firm). Not itself RLS-scoped — access is controlled separately.</summary>
public sealed class OfficeEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>A disabled office cannot sign in (demo-approval / suspension gate).</summary>
    public bool Disabled { get; set; }

    /// <summary>When true, every office user must complete Korkut TOTP at login (KDM-017).</summary>
    public bool RequireTwoFactor { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
