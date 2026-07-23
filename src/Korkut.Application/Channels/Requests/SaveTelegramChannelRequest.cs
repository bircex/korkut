namespace Korkut.Application.Channels.Requests;

/// <summary>Body for configuring the Telegram channel.</summary>
public sealed record SaveTelegramChannelRequest
{
    public bool Enabled { get; init; }

    /// <summary>null → keep the stored token; "" → clear it; a value → replace it.</summary>
    public string? BotToken { get; init; }
}
