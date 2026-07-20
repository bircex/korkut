namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A tenant-owned messaging-channel configuration (one row per office per kind:
/// email / telegram / whatsapp). The provider secret (SMTP app password, Telegram bot
/// token, WhatsApp token) is stored ENCRYPTED via <c>ISecretEncryptor</c> and never
/// returned to a client — only a <c>configured</c> boolean is exposed. Also carries the
/// external-account link state (a pairing code and, once paired, the linked id).
/// </summary>
public sealed class ChannelEntity : ITenantOwned
{
    public Guid OfficeId { get; set; }

    /// <summary>"email" | "telegram" | "whatsapp". Part of the composite key.</summary>
    public string Kind { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    // Email-specific.
    public string? FromAddress { get; set; }

    public string? FromName { get; set; }

    // WhatsApp-specific.
    public string? BaseUrl { get; set; }

    /// <summary>Encrypted provider secret (never returned). Null = not configured.</summary>
    public string? EncryptedSecret { get; set; }

    /// <summary>An external account has completed pairing.</summary>
    public bool Linked { get; set; }

    /// <summary>The paired external id (e.g. a Telegram chat id). Set by the pairing flow.</summary>
    public string? LinkedId { get; set; }

    /// <summary>The current one-time pairing code (cleared once used or unlinked).</summary>
    public string? LinkCode { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
