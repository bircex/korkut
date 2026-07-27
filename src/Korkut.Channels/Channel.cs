using Korkut.Core;

namespace Korkut.Channels;

/// <summary>
/// A messaging-channel configuration: one row per office per kind (email / telegram /
/// whatsapp), plus the external-account pairing state. The provider secret is held only in
/// its encrypted form — the aggregate never sees plaintext and never hands the value back
/// out, so callers can learn that a channel is configured (<see cref="HasSecret"/>) but
/// never what it was configured with.
/// </summary>
/// <remarks>
/// Identity here is the (office, kind) pair, not a surrogate id, so this aggregate does not
/// derive from <see cref="Entity"/> and its repository is a purpose-built port rather than
/// <c>IRepository&lt;T&gt;</c>. The table has always been keyed this way; keeping it avoids
/// a migration for no gain.
/// </remarks>
public sealed class Channel : ITenantOwned, IAggregateRoot
{
    public Channel(Guid officeId, string kind)
    {
        OfficeId = officeId;
        Kind = kind;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Channel()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>"email" | "telegram" | "whatsapp". Together with the office, the key.</summary>
    public string Kind { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    /// <summary>Email only: the envelope sender.</summary>
    public string? FromAddress { get; private set; }

    /// <summary>Email only: the display name.</summary>
    public string? FromName { get; private set; }

    /// <summary>WhatsApp only: the provider gateway.</summary>
    public string? BaseUrl { get; private set; }

    /// <summary>The provider secret, encrypted at rest. Null = not configured.</summary>
    public string? EncryptedSecret { get; private set; }

    /// <summary>An external account has completed pairing.</summary>
    public bool Linked { get; private set; }

    /// <summary>The paired external id (e.g. a Telegram chat id). Set by the pairing flow.</summary>
    public string? LinkedId { get; private set; }

    /// <summary>The current one-time pairing code (cleared once used or unlinked).</summary>
    public string? LinkCode { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Whether a provider secret is stored. The value itself is never exposed.</summary>
    public bool HasSecret => EncryptedSecret is not null;

    public void ConfigureEmail(bool enabled, string? fromAddress, string? fromName, DateTimeOffset updatedAt)
    {
        Enabled = enabled;
        FromAddress = fromAddress;
        FromName = fromName;
        UpdatedAt = updatedAt;
    }

    public void ConfigureTelegram(bool enabled, DateTimeOffset updatedAt)
    {
        Enabled = enabled;
        UpdatedAt = updatedAt;
    }

    public void ConfigureWhatsApp(bool enabled, string? baseUrl, DateTimeOffset updatedAt)
    {
        Enabled = enabled;
        BaseUrl = baseUrl;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Applies a secret change whose <see cref="SecretUpdate.NewValue"/> is already
    /// encrypted: keep leaves the stored value alone, clear drops it, anything else replaces it.
    /// </summary>
    public void ApplyEncryptedSecret(SecretUpdate update)
    {
        if (update.Clear)
        {
            EncryptedSecret = null;
        }
        else if (!update.Keep && update.NewValue is not null)
        {
            EncryptedSecret = update.NewValue;
        }
    }

    /// <summary>Starts a fresh pairing: a new one-time code drops any existing pairing.</summary>
    public void StartLink(string code, DateTimeOffset updatedAt)
    {
        LinkCode = code;
        Linked = false;
        LinkedId = null;
        UpdatedAt = updatedAt;
    }

    /// <summary>Drops the pairing and any outstanding code.</summary>
    public void Unlink(DateTimeOffset updatedAt)
    {
        Linked = false;
        LinkedId = null;
        LinkCode = null;
        UpdatedAt = updatedAt;
    }
}
