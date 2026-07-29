using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Channels;

/// <summary>Body for configuring the office e-mail channel.</summary>
public sealed record SaveEmailChannelRequest
{
    public bool Enabled { get; init; }

    [MaxLength(320, ErrorMessage = "E-posta adresi en fazla 320 karakter olabilir.")]
    public string FromAddress { get; init; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Gönderen adı en fazla 200 karakter olabilir.")]
    public string FromName { get; init; } = string.Empty;

    /// <summary>null → keep the stored password; "" → clear it; a value → replace it.</summary>
    public string? AppPassword { get; init; }
}

/// <summary>Body for configuring the Telegram channel.</summary>
public sealed record SaveTelegramChannelRequest
{
    public bool Enabled { get; init; }

    /// <summary>null → keep the stored token; "" → clear it; a value → replace it.</summary>
    public string? BotToken { get; init; }
}

/// <summary>Body for configuring the WhatsApp channel.</summary>
public sealed record SaveWhatsAppChannelRequest
{
    public bool Enabled { get; init; }

    [MaxLength(400, ErrorMessage = "Adres en fazla 400 karakter olabilir.")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>null → keep the stored token; "" → clear it; a value → replace it.</summary>
    public string? Token { get; init; }
}
