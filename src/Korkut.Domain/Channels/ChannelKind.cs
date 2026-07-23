namespace Korkut.Domain.Channels;

/// <summary>The messaging-channel kinds an office can configure.</summary>
public static class ChannelKind
{
    public const string Email = "email";
    public const string Telegram = "telegram";
    public const string WhatsApp = "whatsapp";

    /// <summary>The kinds an office sees, in the order the integrations screen lists them.</summary>
    public static readonly IReadOnlyList<string> All = [Telegram, WhatsApp, Email];

    /// <summary>Case-insensitive parse of a caller-supplied kind; null when it is not one of ours.</summary>
    public static string? Normalize(string? kind) => kind?.ToLowerInvariant() switch
    {
        Email => Email,
        Telegram => Telegram,
        WhatsApp => WhatsApp,
        _ => null,
    };
}
