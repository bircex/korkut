namespace Korkut.Domain.Channels;

/// <summary>The messaging-channel kinds an office can configure.</summary>
public static class ChannelKind
{
    public const string Email = "email";
    public const string Telegram = "telegram";
    public const string WhatsApp = "whatsapp";
}

/// <summary>
/// A messaging-channel configuration (domain model). The provider secret never leaves
/// persistence — its presence is surfaced only as <see cref="HasSecret"/>.
/// </summary>
public sealed record Channel(
    string Kind, bool Enabled, string? FromAddress, string? FromName, string? BaseUrl,
    bool Linked, string? LinkedId, bool HasSecret);

/// <summary>
/// How a secret field on a channel should change: keep the stored value, clear it, or
/// set a new plaintext value (which the adapter encrypts). Mirrors the UI contract where
/// the secret is <c>null</c> to keep, <c>""</c> to clear, or a value to replace.
/// </summary>
public readonly record struct SecretUpdate(bool Keep, bool Clear, string? NewValue)
{
    public static SecretUpdate From(string? raw) => raw switch
    {
        null => new SecretUpdate(true, false, null),
        "" => new SecretUpdate(false, true, null),
        _ => new SecretUpdate(false, false, raw),
    };
}
