using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// A single platform-wide, admin-controlled setting (cross-tenant; no office_id, no RLS). Kept as a
/// narrow key/value row rather than a one-row typed table so a new dial costs a key, not a migration.
/// Values are operational config an admin may flip at runtime — never secrets: API keys stay in the
/// environment (see <see cref="RuntimeConfig"/>).
/// </summary>
public sealed class PlatformSetting : Entity, IAggregateRoot
{
    /// <summary>The catalog key every office without its own enabled assignment resolves to.</summary>
    public const string DefaultModelKey = "default_model_key";

    public PlatformSetting(string key, string? value, DateTimeOffset now)
        : base(NewId())
    {
        Key = key;
        Value = value;
        UpdatedAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private PlatformSetting()
    {
    }

    public string Key { get; private set; } = string.Empty;

    public string? Value { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Set(string? value, DateTimeOffset now)
    {
        Value = value;
        UpdatedAt = now;
    }
}
