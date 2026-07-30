using Korkut.Core;

namespace Korkut.Settings.Entities;

/// <summary>
/// Per-office mail preference. The system sends mail from ONE operator-controlled account
/// (no per-office e-mail integration); this only records where the office wants notification
/// mail delivered and whether informational mail is enabled at all. Security/invite mail is
/// always sent regardless of this toggle.
/// </summary>
/// <remarks>
/// Keyed by the office (exactly one row per office), so — like <see cref="Persona"/> — this
/// aggregate does not derive from <see cref="Entity"/> and its port is written out instead
/// of <c>IRepository&lt;T&gt;</c>.
/// </remarks>
public sealed class MailPreference : ITenantOwned, IAggregateRoot
{
    public MailPreference(Guid officeId) => OfficeId = officeId;

    /// <summary>Materialization constructor for the ORM.</summary>
    private MailPreference()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>Whether informational/notification mail is enabled for this office.</summary>
    public bool NotificationsEnabled { get; private set; } = true;

    /// <summary>Where notification mail is delivered; null falls back to the office owner's e-mail.</summary>
    public string? NotifyEmail { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(bool notificationsEnabled, string? notifyEmail, DateTimeOffset updatedAt)
    {
        NotificationsEnabled = notificationsEnabled;
        NotifyEmail = string.IsNullOrWhiteSpace(notifyEmail) ? null : notifyEmail.Trim();
        UpdatedAt = updatedAt;
    }
}
