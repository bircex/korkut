using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Channels.Requests;

/// <summary>Body for configuring the WhatsApp channel.</summary>
public sealed record SaveWhatsAppChannelRequest
{
    public bool Enabled { get; init; }

    [MaxLength(400, ErrorMessage = "Adres en fazla 400 karakter olabilir.")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>null → keep the stored token; "" → clear it; a value → replace it.</summary>
    public string? Token { get; init; }
}
